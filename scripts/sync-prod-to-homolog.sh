#!/usr/bin/env bash
# Copies prod data (PostgreSQL + R2 bucket) into homolog.
# Reads credentials from .env.prod (prod) and .env (homolog) at the repo root.
# Requirements: pg_dump, pg_restore, psql, aws CLI
set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'
info()    { echo -e "${GREEN}▸${NC} $*"; }
warn()    { echo -e "${YELLOW}⚠${NC}  $*"; }
success() { echo -e "${GREEN}✓${NC} $*"; }
die()     { echo -e "${RED}✗${NC} $*" >&2; exit 1; }

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# ── Preflight checks ──────────────────────────────────────────────────────────
[[ -f "$ROOT/.env.prod" ]] || die ".env.prod not found at $ROOT/.env.prod"
[[ -f "$ROOT/.env"      ]] || die ".env not found at $ROOT/.env"
command -v pg_dump    &>/dev/null || die "pg_dump not found — run: brew install libpq && echo 'export PATH=\"/opt/homebrew/opt/libpq/bin:\$PATH\"' >> ~/.zshrc && source ~/.zshrc"
command -v pg_restore &>/dev/null || die "pg_restore not found — same fix as pg_dump"
command -v psql       &>/dev/null || die "psql not found — same fix as pg_dump"
command -v aws        &>/dev/null || die "aws CLI not found — run: brew install awscli"

# ── Credential parsing ────────────────────────────────────────────────────────
# Read a key=value pair from a dotenv file (ignores comments and blank lines)
dotenv() { grep -v '^[[:space:]]*#' "$1" | grep -v '^[[:space:]]*$' | grep "^${2}=" | head -1 | cut -d'=' -f2- || true; }

# Extract a field from an Npgsql-style connection string (Key=Value;Key=Value;...)
cs_field() { echo "$1" | tr ';' '\n' | grep -i "^[[:space:]]*${2}[[:space:]]*=" | head -1 | sed 's/[^=]*=\s*//' | xargs || true; }

PROD_CS=$(dotenv "$ROOT/.env.prod" "ConnectionStrings__DefaultConnection")
HOM_CS=$(dotenv  "$ROOT/.env"      "ConnectionStrings__DefaultConnection")

[[ -n "$PROD_CS" ]] || die "ConnectionStrings__DefaultConnection missing in .env.prod"
[[ -n "$HOM_CS"  ]] || die "ConnectionStrings__DefaultConnection missing in .env"

PROD_HOST=$(cs_field "$PROD_CS" "Host");     PROD_DB=$(cs_field "$PROD_CS" "Database")
PROD_USER=$(cs_field "$PROD_CS" "Username"); PROD_PASS=$(cs_field "$PROD_CS" "Password")

HOM_HOST=$(cs_field "$HOM_CS" "Host");      HOM_DB=$(cs_field "$HOM_CS" "Database")
HOM_USER=$(cs_field "$HOM_CS" "Username");  HOM_PASS=$(cs_field "$HOM_CS" "Password")

[[ -n "$PROD_HOST" && -n "$PROD_DB" && -n "$PROD_USER" && -n "$PROD_PASS" ]] || die "Could not parse prod DB connection string. Check .env.prod"
[[ -n "$HOM_HOST"  && -n "$HOM_DB"  && -n "$HOM_USER"  && -n "$HOM_PASS"  ]] || die "Could not parse homolog DB connection string. Check .env"

PROD_R2_KEY=$(dotenv    "$ROOT/.env.prod" "R2__AccessKeyId")
PROD_R2_SECRET=$(dotenv "$ROOT/.env.prod" "R2__SecretAccessKey")
PROD_R2_BUCKET=$(dotenv "$ROOT/.env.prod" "R2__BucketName")
PROD_R2_ACCOUNT=$(dotenv "$ROOT/.env.prod" "R2__AccountId")

HOM_R2_KEY=$(dotenv    "$ROOT/.env" "R2__AccessKeyId")
HOM_R2_SECRET=$(dotenv "$ROOT/.env" "R2__SecretAccessKey")
HOM_R2_BUCKET=$(dotenv "$ROOT/.env" "R2__BucketName")
HOM_R2_ACCOUNT=$(dotenv "$ROOT/.env" "R2__AccountId")

[[ -n "$PROD_R2_KEY" && -n "$PROD_R2_SECRET" && -n "$PROD_R2_BUCKET" && -n "$PROD_R2_ACCOUNT" ]] || die "Missing R2 credentials in .env.prod"
[[ -n "$HOM_R2_KEY"  && -n "$HOM_R2_SECRET"  && -n "$HOM_R2_BUCKET"  && -n "$HOM_R2_ACCOUNT"  ]] || die "Missing R2 credentials in .env"

# ── Confirm ───────────────────────────────────────────────────────────────────
echo ""
warn "This will OVERWRITE all homolog data with prod data."
warn "  DB:  ${PROD_DB}@${PROD_HOST}"
warn "    →  ${HOM_DB}@${HOM_HOST}"
warn "  R2:  s3://${PROD_R2_BUCKET}  →  s3://${HOM_R2_BUCKET}"
echo ""
read -rp "Type YES to continue: " CONFIRM
[[ "$CONFIRM" == "YES" ]] || { echo "Aborted."; exit 0; }

# ── Cleanup on exit ───────────────────────────────────────────────────────────
DUMP_FILE=""
STAGING_DIR=""
cleanup() {
    [[ -n "$DUMP_FILE"   && -f "$DUMP_FILE"   ]] && rm -f "$DUMP_FILE"
    [[ -n "$STAGING_DIR" && -d "$STAGING_DIR" ]] && rm -rf "$STAGING_DIR"
}
trap cleanup EXIT

# ── Database ──────────────────────────────────────────────────────────────────
echo ""
info "Dumping prod database..."
DUMP_FILE=$(mktemp /tmp/prod_dump.XXXXXX)

PGPASSWORD="$PROD_PASS" PGSSLMODE=require \
  pg_dump -h "$PROD_HOST" -U "$PROD_USER" -d "$PROD_DB" \
  --no-owner --no-acl -F c -f "$DUMP_FILE"

info "Wiping homolog schema..."
PGPASSWORD="$HOM_PASS" PGSSLMODE=require \
  psql -h "$HOM_HOST" -U "$HOM_USER" -d "$HOM_DB" -q \
  -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"

info "Restoring to homolog..."
PGPASSWORD="$HOM_PASS" PGSSLMODE=require \
  pg_restore -h "$HOM_HOST" -U "$HOM_USER" -d "$HOM_DB" \
  --no-owner --no-acl "$DUMP_FILE"

success "Database synced."

# ── R2 bucket ─────────────────────────────────────────────────────────────────
echo ""
PROD_ENDPOINT="https://${PROD_R2_ACCOUNT}.r2.cloudflarestorage.com"
HOM_ENDPOINT="https://${HOM_R2_ACCOUNT}.r2.cloudflarestorage.com"
STAGING_DIR=$(mktemp -d /tmp/r2_sync.XXXXXX)

info "Downloading from prod s3://${PROD_R2_BUCKET}..."
AWS_ACCESS_KEY_ID="$PROD_R2_KEY" \
AWS_SECRET_ACCESS_KEY="$PROD_R2_SECRET" \
AWS_DEFAULT_REGION="auto" \
  aws s3 sync "s3://${PROD_R2_BUCKET}" "$STAGING_DIR" \
  --endpoint-url "$PROD_ENDPOINT" --no-progress

info "Uploading to homolog s3://${HOM_R2_BUCKET}..."
AWS_ACCESS_KEY_ID="$HOM_R2_KEY" \
AWS_SECRET_ACCESS_KEY="$HOM_R2_SECRET" \
AWS_DEFAULT_REGION="auto" \
  aws s3 sync "$STAGING_DIR" "s3://${HOM_R2_BUCKET}" \
  --endpoint-url "$HOM_ENDPOINT" --no-progress

success "R2 bucket synced."
echo ""
success "Done — homolog is now a copy of prod."
