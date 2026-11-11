# Contrato HTTP · 003 · Contas a pagar e baixas

API JSON de mesma origem. Reutilizar [sessão, HTTPS, CSRF e formato de erros da fundação](../../001-financial-foundation/contracts/api.md). Toda operação exige sessão, e toda mutação exige `X-XSRF-TOKEN`. O servidor obtém o titular da sessão. Respostas financeiras usam `Cache-Control: no-store`.

## Obrigações

| Operação | Entrada | Sucesso |
|---|---|---|
| `GET /api/bills` | Filtros e paginação abaixo. | `200`, página, contagem e total em aberto. |
| `POST /api/bills` | `description`, `amount`, `dueDate`. | `201`, detalhe, `Location` e ETag. |
| `GET /api/bills/{id}` | UUID da obrigação. | `200`, detalhe e ETag. |
| `PUT /api/bills/{id}` | `If-Match` e campos do cadastro. | `200`, detalhe e nova ETag. |
| `PUT /api/bills/{id}/status` | `If-Match` e `{"status":"canceled"}`. | `200`, detalhe e nova ETag. |

Valores monetários são strings decimais invariantes. Na entrada, rejeitar expoente, separador de milhares, valor não positivo e mais de duas casas. Valores de resposta têm duas casas. Datas civis usam `YYYY-MM-DD`. Itens e detalhe contêm `id`, campos do cadastro, `currency` (`BRL`) e `status`, cujo valor é `upcoming`, `dueToday`, `overdue`, `paid` ou `canceled`; `open` e `all` são apenas valores de filtro. O detalhe também contém `activeSettlement`, nulo em ausência de baixa ou com o recibo da baixa ativa.

A ETag forte do detalhe inclui a revisão persistida e a situação derivada. Uma mudança de situação à meia-noite invalida uma ETag anterior. Mutações da obrigação aceitam uma única tag forte, entre aspas, em `If-Match`. Após conflito de versão, a interface orienta a recarregar o detalhe ou a selecionar novamente a despesa.

### Filtros e total

- `status` aceita `open` (padrão), `upcoming`, `dueToday`, `overdue`, `paid`, `canceled` ou `all`. `open` reúne as três situações temporais em aberto. Com `all`, o total ainda soma somente obrigações abertas que passam pelo período.
- `from` e `to` limitam o vencimento de forma inclusiva. Omitir ambas para consultar sem limite de período, ou enviar ambas com `from <= to`.
- `page` e `pageSize` seguem a convenção da [feature 002](../../002-transactions/contracts/api.md#filtros-e-paginação). Ordenar por `dueDate ASC, id ASC`. A resposta contém `items`, `page`, `pageSize`, `totalCount` e `totals.openAmount`. A soma considera todas as obrigações em aberto que passam pelo filtro, mesmo fora da página. Filtro vazio ou só de pagas ou canceladas retorna `"0.00"`. Página além do fim retorna lista vazia e preserva contagem e total. Cada resposta é consistente internamente.

## Baixas

| Operação | Entrada | Sucesso |
|---|---|---|
| `GET /api/bills/{id}/eligible-expenses` | Obrigação em aberto, paginação e par opcional `from`/`to` aplicado à data da movimentação. | `200`, página de despesas elegíveis. |
| `POST /api/bills/{id}/settlements` | `Idempotency-Key` com UUID não vazio e um dos corpos abaixo. | `201` na baixa e `200` no reenvio, com o mesmo recibo. |
| `PUT /api/bills/{id}/settlements/{settlementId}/status` | `{"status":"reversed"}`. | `204`, inclusive se aquela baixa já foi revertida. |

A consulta de despesas devolve `items`, `page`, `pageSize` e `totalCount`, com ordenação `transactionDate DESC, id DESC`. Cada item contém `transactionId`, `transactionVersion` (ETag da movimentação), `accountId`, `accountArchived`, `amount`, `description` e `transactionDate`. Inclui somente despesas ativas do titular, de valor idêntico e sem baixa ativa, inclusive em contas arquivadas. Sem datas, consulta todo o histórico. A seleção não reserva a despesa.

- `mode: "link"` recebe `billVersion`, `transactionId` e `transactionVersion`.
- `mode: "create"` recebe `billVersion`, `accountId` e `paymentDate`. O servidor copia valor e descrição. A data pode anteceder ou suceder o vencimento, até o dia atual de São Paulo.

`billVersion` e `transactionVersion` são as tags fortes obtidas na leitura, incluindo suas aspas no valor JSON. As duas formas de corpo são exclusivas. O recibo contém `id`, `billId`, `transactionId`, `origin` (`linkedExpense` ou `createdExpense`) e `createdAtUtc`. É imutável, inclusive depois de revertido. Após criar, repetir ou reverter, a SPA recarrega o detalhe e a consulta. Em resultado incerto, reenvia a mesma chave e corpo. Outra baixa intencional exige nova chave. A reversão é endereçada ao ID da baixa para evitar atingir um vínculo posterior.

## Erros e integração com movimentações

Usar `application/problem+json` com `status`, `title`, `code` e, quando aplicável, `detail` ou `errors`. Mensagens em português.

| HTTP | Código | Condição |
|---|---|---|
| 400 | `VALIDATION_FAILED` | Corpo, filtros, paginação, valor, descrição ou data inválidos. |
| 400 | `INVALID_IDEMPOTENCY_KEY` | Chave ausente, vazia, inválida ou cabeçalho repetido. |
| 400 | `INVALID_BILL_VERSION` | `billVersion` ausente ou inválida no corpo da baixa, ou `If-Match` da obrigação malformado. |
| 400 | `INVALID_TRANSACTION_VERSION` | `transactionVersion` ausente ou inválida no corpo da baixa vinculada. |
| 401, 403 | `SESSION_REQUIRED`, `INVALID_CSRF_TOKEN` | Convenções da fundação. |
| 404 | `BILL_NOT_FOUND`, `SETTLEMENT_NOT_FOUND`, `ACCOUNT_NOT_FOUND`, `TRANSACTION_NOT_FOUND` | Recurso ausente ou fora da titularidade, inclusive baixa pertencente a outra obrigação. |
| 409 | `BILL_NOT_OPEN` | A obrigação está paga ou cancelada e a operação exige que esteja em aberto. |
| 409 | `EXPENSE_NOT_ELIGIBLE` | Movimentação ativa e do tipo despesa com valor igual e sem baixa ativa é requisito do vínculo. |
| 409 | `ACCOUNT_ARCHIVED` | Criação de despesa em conta arquivada. |
| 409 | `IDEMPOTENCY_KEY_CONFLICT` | A chave já pertence a outro pedido normalizado. |
| 409 | `TRANSACTION_LINKED` | Correção ou cancelamento de movimentação bloqueado por baixa ativa. |
| 412 | `STALE_BILL_VERSION` | A ETag enviada em `If-Match` é diferente da atual. |
| 409 | `STALE_BILL_VERSION`, `STALE_TRANSACTION_VERSION` | Uma revisão enviada no corpo da baixa é diferente da atual. |
| 428 | `BILL_VERSION_REQUIRED` | Ausência de `If-Match` em edição ou cancelamento de obrigação. |
| 500 | `INTERNAL_ERROR` | Falha inesperada, sem detalhes internos. |

O [contrato de movimentações](../../002-transactions/contracts/api.md) passa a devolver também `source: "billSettlement"` em leituras. As rotas de correção e cancelamento de movimentações rejeitam baixa ativa com `409 TRANSACTION_LINKED` quando a revisão enviada ainda é atual. Uma revisão desatualizada continua retornando `412`. A reversão libera a despesa vinculada anteriormente, conforme as regras da 002, ou mantém a despesa gerada visível como cancelada.
