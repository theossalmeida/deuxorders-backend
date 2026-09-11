# Filtros de pedidos e receita

`GET /api/v1/orders/all`, os quatro agregados de `/dashboard` e
`/dashboard/export` aceitam `from`, `to`, `dateField`, `status`, `clientId` e
`isPaid`. `orders/all` também aceita `productId` (item não cancelado), texto e
paginação. Nenhum filtro de pagamento é aplicado quando `isPaid` é omitido.

- `dateField=DeliveryDate` é o padrão. `CreatedAt` é uma escolha explícita.
- Datas `AAAA-MM-DD` incluem todo o primeiro e último dia em America/Sao_Paulo.
- Timestamps com fuso mantêm os instantes fornecidos: início inclusivo, fim exclusivo.
- Intervalos invertidos e combinações ambíguas de parâmetros são rejeitados.
- O gráfico diário usa o mesmo campo de data e o dia comercial de São Paulo.
- `products/{id}/stats?month=AAAA-MM` usa entrega; aceita `dateField=CreatedAt`.
- A resposta de pedidos inclui `createdAt` além de `deliveryDate`.

O dashboard mantém `deliveryDateFrom/To` (instantes UTC, fim exclusivo), usados
pelo site, e `startDate/endDate`. O par explícito `createdAtFrom/To` seleciona
criação. Não misture pares de parâmetros na mesma chamada.

Exemplos:

```text
/dashboard/summary?from=2026-09-01&to=2026-09-30
/dashboard/summary?from=2026-09-01&to=2026-09-30&dateField=CreatedAt
/orders/all?from=2026-09-11&to=2026-09-11&isPaid=false&status=Received
```

Receita, descontos, ticket e tratamento de cancelamentos mantêm as regras do
SaaS. `totalOrders` do dashboard exclui cancelados; `canceledOrders` é separado.
A listagem de pedidos inclui cancelados quando não há filtro de situação.
Exportação mantém uma linha por item não cancelado e seus limites existentes.
Caixa permanece por data de competência: lançamentos manuais não têm entrega.

Validação: `OrderDateFilterTests` cobre entrega/criação distintas, fronteiras
do dia, pagamento, cliente, produto, parâmetros do SaaS, rankings e CSV. O CSV
é exercitado com PostgreSQL no fluxo CI existente.
