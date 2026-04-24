# Tests Review

Data da auditoria: 23 de abril de 2026

## Escopo

Esta auditoria revalidou a suíte de testes atual do projeto `deuxorders-backend` com foco em:

- cobertura real de regras de negócio
- aderência a cenários próximos da produção
- edge cases relevantes para pedidos, estoque e caixa
- sinais de falso positivo
- sinais de código ou infraestrutura ajustados apenas para "fazer teste passar"

Comando executado:

```powershell
dotnet test .\DeuxERP.Tests\DeuxERP.Tests.csproj -p:UseAppHost=false
```

Resultado observado no dia 23/04/2026:

- 42 testes aprovados
- 0 falhas
- 0 ignorados

O status verde é real, mas a suíte ainda não garante totalmente o comportamento de produção. O principal problema não é ausência completa de testes, e sim a diferença entre o ambiente testado e o ambiente real em pontos críticos de persistência e integração.

## Resumo Executivo

Hoje a suíte cobre razoavelmente bem:

- fluxo básico de pedidos
- validações de pagamento
- parte relevante da lógica de estoque com receita
- relatórios simples de cliente e produto
- parte do módulo de caixa

Hoje a suíte não cobre bem:

- comportamento relacional do banco real
- unicidade e idempotência real de eventos de caixa
- concorrência otimista via `xmin`
- falhas de storage e caminhos de compensação
- consultas específicas de PostgreSQL
- isolamento forte entre testes
- transições de status mais sensíveis do domínio de pedidos

Não foi encontrada manipulação explícita no código produtivo para passar em testes. O risco atual é mais sutil: a infraestrutura de teste é permissiva demais, então alguns bugs reais não teriam como aparecer.

## Achados

### 1. Alta: a suíte roda em uma infraestrutura diferente demais da produção

#### Evidência

- `DeuxERP.Tests/IntegrationTestFactory.cs`
  - substitui `IStorageService` por `NullStorageService`
  - substitui o banco por `UseInMemoryDatabase`
  - ignora `TransactionIgnoredWarning`
- `DeuxERP.Infrastructure/Data/ApplicationDBContext.cs`
  - possui índices únicos
  - possui `DeleteBehavior.Restrict`
  - possui concorrência por `xmin`
  - possui filtro único de caixa em `Source + SourceId`

Trechos relevantes:

- `DeuxERP.Tests/IntegrationTestFactory.cs:15`
- `DeuxERP.Tests/IntegrationTestFactory.cs:49`
- `DeuxERP.Infrastructure/Data/ApplicationDBContext.cs:61`
- `DeuxERP.Infrastructure/Data/ApplicationDBContext.cs:81`
- `DeuxERP.Infrastructure/Data/ApplicationDBContext.cs:101`
- `DeuxERP.Infrastructure/Data/ApplicationDBContext.cs:224`

#### Impacto

Isso impede a suíte de validar de forma confiável:

- violação de unicidade
- comportamento real de `foreign keys`
- `restrict delete`
- concorrência otimista
- diferenças de tradução SQL
- comportamento de transação
- falhas de armazenamento e compensação

Em outras palavras: os testes ficam verdes, mas parte dos riscos mais caros só apareceria em homologação ou produção.

#### O que ajustar

- criar uma camada de testes de integração relacional usando PostgreSQL real ou containerizado
- manter `InMemory` apenas para cenários muito rápidos e sem regras relacionais
- parar de ignorar o warning de transação como substituto de cobertura
- separar claramente:
  - testes rápidos de aplicação
  - testes de integração reais com banco relacional

#### Prioridade

Muito alta. Este é o maior ponto de falso positivo da suíte.

### 2. Alta: idempotência de pagamento não está sendo realmente provada

#### Evidência

- `DeuxERP.Tests/Sales/OrderPaymentTests.cs:65`
  - o teste `MarkAsPaid_Twice_IsIdempotent` apenas verifica `200 OK` na segunda chamada
- a proteção real contra duplicidade depende de:
  - índice único em `cash_flow_entries`
  - captura de `DbUpdateException` por violação de unicidade

Trechos relevantes:

- `DeuxERP.Infrastructure/Data/ApplicationDBContext.cs:224`
- `DeuxERP.Infrastructure/Cash/Handlers/OrderPaidEventHandler.cs:31`
- `DeuxERP.Infrastructure/Cash/Handlers/OrderPaymentReversedEventHandler.cs:31`

#### Impacto

Hoje é possível que:

- a segunda chamada continue retornando `200`
- duas entradas de caixa sejam geradas em banco real
- a suíte continue verde porque `InMemory` não replica esse comportamento

#### O que ajustar

- reescrever o teste de idempotência para validar o estado final, não só o status HTTP
- após chamar `/pay` duas vezes:
  - garantir que existe exatamente uma entrada `OrderPayment` para aquele `OrderId`
  - garantir que não há duplicidade em `Source + SourceId`
- repetir a mesma ideia para `unpay`
- rodar esse teste em banco relacional real

#### Prioridade

Muito alta. Caixa e pagamento são áreas sensíveis e com impacto financeiro direto.

### 3. Alta: transições de status de pedido estão pouco protegidas e podem estar codificando comportamento errado

#### Evidência

- `DeuxERP.Domain/Sales/Order.cs:39`
  - `MarkAsCompleted` tem guards comentadas
- `DeuxERP.Domain/Sales/Order.cs:66`
  - `UpdateStatus` aceita mudança para qualquer status definido
- `DeuxERP.Tests/Sales/OrderStatusTransitionTests.cs:17`
  - hoje a suíte considera correto reabrir pedidos concluídos e cancelados

#### Impacto

Se reabrir pedido concluído ou cancelado não for regra deliberada de negócio, então:

- a implementação está permissiva demais
- a suíte está consolidando esse comportamento como esperado

Esse tipo de teste é perigoso porque protege uma decisão de domínio potencialmente errada.

#### O que ajustar

- decidir explicitamente a política de transição de status:
  - `Canceled -> Received/Preparing` pode?
  - `Completed -> Preparing/Received` pode?
  - `Canceled -> Completed` pode?
  - `Completed -> Canceled` pode?
- depois dessa definição:
  - endurecer o domínio
  - alinhar `OrderService`
  - reescrever os testes para refletir a regra oficial

#### Prioridade

Muito alta. Pedidos são o núcleo do sistema.

### 4. Média: há testes com assertions enfraquecidas por possível compartilhamento de estado

#### Evidência

- `DeuxERP.Tests/BaseIntegrationTest.cs:9`
  - usa `IClassFixture`
- `DeuxERP.Tests/IntegrationTestFactory.cs:28`
  - usa um banco por fixture
- `DeuxERP.Tests/Cash/CashFlowTests.cs:190`
  - usa `>=` em vez de validar totais exatos

#### Impacto

Esse padrão normalmente aparece quando:

- dados de um teste influenciam o seguinte
- o teste deixa de validar o valor exato para não falhar aleatoriamente

Isso não é trapaça explícita, mas reduz bastante a força da suíte.

#### O que ajustar

- garantir isolamento por teste ou por classe com limpeza explícita
- remover asserts `>=` quando o valor exato puder ser previsto
- preferir massas de dados determinísticas por cenário

#### Prioridade

Média alta. É um problema de confiabilidade da suíte.

### 5. Média: o smoke test de ciclo de vida de pedidos é fraco para garantir regra de negócio

#### Evidência

- `DeuxERP.Tests/OrderIntegrationFlowTest.cs`
  - usa `Task.Delay(100)`
  - usa `Assert.NotEmpty`
  - faz poucas validações de estado final
  - não relê o pedido após cada mutação importante

#### Impacto

Esse teste valida que "o fluxo responde", mas não valida bem que:

- quantidade foi ajustada corretamente
- cancelamento mudou os campos esperados
- total do pedido foi recalculado corretamente
- conclusão preservou regras

#### O que ajustar

- transformar esse arquivo em um teste de fluxo realmente assertivo
- após cada mutação:
  - buscar o pedido novamente
  - validar status
  - validar totais
  - validar itens cancelados
  - validar quantidade final
- remover `Task.Delay`

#### Prioridade

Média.

### 6. Média: caminhos críticos de storage não estão cobertos

#### Evidência

O storage fake em `IntegrationTestFactory.cs` nunca falha. Isso deixa sem cobertura:

- upload de imagem de produto
- rollback/compensação após falha ao salvar produto
- substituição de imagem com falha ao remover imagem antiga
- remoção de imagem com restauração de referência no banco
- remoção de referência de pedido com restauração em caso de falha no storage

Trechos relevantes:

- `DeuxERP.API/Controllers/ProductController.cs:45`
- `DeuxERP.API/Controllers/ProductController.cs:109`
- `DeuxERP.API/Controllers/ProductController.cs:127`
- `DeuxERP.API/Controllers/ProductController.cs:175`
- `DeuxERP.API/Controllers/OrderController.cs:43`

#### Impacto

Os fluxos mais suscetíveis a inconsistência entre banco e storage estão sem teste real.

#### O que ajustar

- criar um fake de storage configurável por cenário, não um fake sempre feliz
- testar explicitamente:
  - falha no upload inicial
  - falha na limpeza após erro de persistência
  - falha ao remover imagem antiga
  - falha ao remover referência de pedido
- validar o estado final do banco após cada falha

#### Prioridade

Média alta.

### 7. Média: consultas específicas de PostgreSQL não estão validadas pela suíte atual

#### Evidência

O código possui caminhos diferentes conforme o provider:

- `DeuxERP.API/Controllers/ClientController.cs:177`
- `DeuxERP.API/Controllers/InventoryController.cs:55`
- `DeuxERP.API/Controllers/ProductController.cs:379`

Nesses pontos, com PostgreSQL, usa `EF.Functions.ILike`. Com `InMemory`, cai no fallback com `Contains`.

#### Impacto

As consultas em produção podem se comportar diferente do que a suíte atual valida:

- case-insensitive de verdade
- tradução SQL
- performance
- comportamento com acentos e collation

#### O que ajustar

- criar testes de busca rodando com PostgreSQL
- validar pelo menos:
  - case-insensitive
  - paginação
  - filtros combinados
  - busca parcial

#### Prioridade

Média.

### 8. Baixa/Média: seed e inspeção direta de banco existem, mas estão relativamente controlados

#### Evidência

- `DeuxERP.Tests/BaseIntegrationTest.cs:34`
  - cria usuário diretamente no banco para autenticação
- `DeuxERP.Tests/InventoryIntegrationTests.cs:133`
  - lê entidade diretamente do banco para verificar saldo

#### Avaliação

Isso, por si só, não é manipulação indevida. É aceitável quando:

- o seed serve só para viabilizar autenticação
- a leitura direta serve para conferir efeito colateral não exposto pela API

#### Ponto de atenção

Esse padrão deve permanecer restrito. Se começar a substituir fluxos reais da API por seed ou leitura direta excessiva, a suíte perde valor.

## Cobertura Boa Hoje

Apesar dos gaps, há partes boas e úteis na suíte.

### Estoque

`DeuxERP.Tests/InventoryIntegrationTests.cs` cobre bem:

- CRUD básico de material
- média ponderada de reposição
- receita de produto
- dedução ao entrar em `Preparing`
- warning quando saldo fica negativo
- restauração de estoque ao cancelar pedido
- restauração seletiva ao cancelar item
- ajuste de estoque por delta de quantidade
- ignorar produtos sem receita

Esta é hoje a área mais sólida da suíte.

### Pagamento e Caixa

`DeuxERP.Tests/Sales/OrderPaymentTests.cs` e `DeuxERP.Tests/Cash/CashFlowTests.cs` cobrem:

- pagamento de pedido válido
- pedido cancelado não pode ser pago
- pedido com valor zero não pode ser pago
- `unpay` exige motivo
- usuário não-admin não pode pagar
- pagamento gera entrada de caixa
- reversão gera saída de caixa
- lançamentos automáticos não podem ser editados/deletados
- soft delete de lançamento manual

A cobertura funcional existe, mas precisa ser endurecida no que depende do banco real.

### Relatórios simples

`ClientStatsTests`, `ClientOrderHistoryTests`, `ClientListTotalsTests` e `ProductStatsTests` dão boa proteção para:

- exclusão de pedidos cancelados nos totais
- respostas `404`
- cenários de zero resultado
- estatísticas mensais básicas

## O que Não Foi Encontrado

Não foram encontrados sinais claros de:

- `#if TEST`
- caminhos de produção condicionados para ambiente de teste
- bypass explícito de regra apenas para testes
- hooks artificiais no domínio para satisfazer a suíte

Ou seja: não há indício de fraude arquitetural para passar teste. O problema é de qualidade e realismo da suíte, não de manipulação explícita do código produtivo.

## Ajustes Recomendados

## Fase 1: endurecer a infraestrutura dos testes

- adicionar uma suíte de integração com PostgreSQL real
- usar migrations reais nessa suíte
- manter `InMemory` apenas onde a regra não depende de banco relacional
- tornar o storage fake configurável para sucesso e falha
- remover dependência de `Task.Delay`

## Fase 2: corrigir pontos frágeis do domínio de pedidos

- definir oficialmente a matriz de transição de status
- revisar `Order.MarkAsCompleted`
- revisar `Order.UpdateStatus`
- reescrever `OrderStatusTransitionTests`
- revisar se reabertura de pedido é regra ou bug

## Fase 3: endurecer caixa e idempotência

- validar exatamente uma entrada automática por pagamento
- validar exatamente uma reversão por `unpay`
- criar teste de pagamento repetido em banco relacional
- criar teste de reversão repetida
- validar contagem de registros e não só `200 OK`

## Fase 4: eliminar falso verde por estado compartilhado

- isolar banco por teste ou limpar dados entre testes
- remover asserts amplos como `>=`
- usar massas de dados determinísticas
- garantir que cada teste conhece exatamente o estado inicial

## Fase 5: aumentar qualidade dos testes de fluxo

- reescrever `OrderIntegrationFlowTest` com asserts fortes
- reler estado após cada mutação
- validar totais, quantidade, itens cancelados e status
- validar warnings quando aplicável

## Matriz de Implementação para o Agente Desenvolvedor

### Bloco 1: infraestrutura de testes

Objetivo:

- reduzir falso positivo estrutural

Implementar:

- nova factory para testes com PostgreSQL real
- estratégia de criação/limpeza de banco para cada execução
- storage fake configurável por cenário

Critério de aceite:

- testes de unicidade, `restrict delete`, busca e idempotência rodam contra provider relacional

### Bloco 2: pedidos

Objetivo:

- formalizar regras de transição de status

Implementar:

- matriz de transições válidas
- validações no domínio
- atualização dos testes existentes
- novos testes para transições inválidas

Critério de aceite:

- a suíte deixa explícito quais transições são permitidas e quais devem falhar

### Bloco 3: caixa e eventos

Objetivo:

- garantir que duplicidade de evento não vira duplicidade financeira

Implementar:

- testes de idempotência com contagem de registros
- testes de pagamento e reversão repetidos
- testes com banco relacional real

Critério de aceite:

- nenhum cenário gera mais de uma entrada automática indevida por pedido

### Bloco 4: storage e compensação

Objetivo:

- validar consistência entre banco e armazenamento

Implementar:

- testes de falha no upload
- testes de falha no delete
- testes de restauração de referência no banco
- testes de resposta HTTP adequada nos cenários de compensação

Critério de aceite:

- após qualquer falha simulada, o estado final do banco permanece consistente

### Bloco 5: consultas e busca

Objetivo:

- validar comportamento real em PostgreSQL

Implementar:

- testes de busca com `ILike`
- filtros por status
- paginação
- combinação de busca + filtro

Critério de aceite:

- o comportamento em teste reflete o comportamento de produção

## Casos de Teste que Precisam Ser Adicionados

### Pedidos

- tentar concluir pedido cancelado
- tentar cancelar pedido concluído
- tentar reabrir pedido concluído
- tentar reabrir pedido cancelado
- tentar alterar quantidade de item cancelado
- tentar cancelar item já cancelado
- atualizar pedido em `Preparing` com inclusão de item novo e validar estoque
- pagar pedido duas vezes e validar uma única entrada de caixa
- desfazer pagamento duas vezes e validar uma única reversão

### Estoque

- receita com material inativo
- ajuste de quantidade que zera ou deixa item inválido
- transição de status sem mudança real não pode deduzir estoque novamente
- pedido com múltiplos itens do mesmo produto não deve duplicar dedução indevida
- comportamento futuro preparado para `LocationId` sem implementar `LocationId` agora

### Caixa

- tentativa de duplicar entrada automática por concorrência
- resumo de caixa com base limpa e valores exatos
- testes de filtro com `includeDeleted`
- validação de `SourceId` em cenários automáticos e manuais

### Produto e Storage

- upload de imagem com tipo inválido
- upload acima do limite
- falha no upload
- falha ao apagar imagem antiga após update
- falha ao apagar imagem em delete

### Banco relacional

- violação de índice único de usuário
- violação de índice único de caixa
- falha ao deletar produto vinculado a pedido
- falha ao deletar client referenciado por pedido
- comportamento de concorrência via `xmin`

## Pontos de Atenção Arquitetural

- não transformar todos os testes em integração pesada; separar por objetivo
- não usar banco real para testes que são puramente de domínio
- não mascarar instabilidade com asserts frouxos
- não introduzir sleeps para esperar efeitos síncronos
- não codificar regra incerta de negócio sem validação explícita com o dono do produto
- ao endurecer a suíte, priorizar os fluxos com impacto financeiro e operacional:
  - pedidos
  - caixa
  - estoque

## Ordem Recomendada de Trabalho

1. criar infraestrutura relacional de testes
2. corrigir e endurecer testes de idempotência de caixa
3. definir e corrigir regras de transição de status de pedidos
4. reescrever o `OrderIntegrationFlowTest`
5. cobrir falhas de storage
6. cobrir consultas específicas de PostgreSQL
7. revisar isolamento e limpeza entre testes

## Conclusão

A suíte atual é útil, mas ainda não é suficiente para ser tratada como proteção forte contra regressão em áreas críticas. O núcleo mais promissor é estoque; os maiores riscos estão em persistência relacional, idempotência financeira, transições de pedido e compensações entre banco e storage.

O foco correto para a próxima rodada não é adicionar mais volume de testes indiscriminadamente. O foco é endurecer a suíte onde hoje ela pode ficar verde mesmo com bug real de produção.
