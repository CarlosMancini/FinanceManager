# Contrato HTTP · 002 · Movimentações manuais

API JSON na mesma origem HTTPS da SPA. Reutiliza [sessão, CSRF e erros da fundação](../../001-financial-foundation/contracts/api.md). Todas as operações exigem sessão. Mutações exigem `X-XSRF-TOKEN`. Respostas financeiras usam `Cache-Control: no-store`. O servidor obtém o titular da sessão e consulta apenas seus dados.

## Operações

| Operação | Entrada | Sucesso |
|---|---|---|
| `POST /api/transactions` | Cabeçalho `Idempotency-Key` com UUID não vazio e corpo de cadastro. | `201` na criação. `200` com a representação e a versão atuais no reenvio equivalente. Ambas retornam o mesmo recurso em `Location` e a ETag da representação devolvida. |
| `GET /api/transactions` | Filtros e paginação descritos abaixo. | `200`, página com totais do filtro. |
| `GET /api/transactions/{id}` | UUID da movimentação. | `200`, representação e ETag. |
| `PUT /api/transactions/{id}` | `If-Match` e os mesmos campos editáveis do cadastro. | `200`, representação atualizada e nova ETag. |
| `PUT /api/transactions/{id}/status` | `If-Match` e `{"status":"canceled"}`. | `200`, representação cancelada e nova ETag. |

Cadastro e edição recebem `accountId`, `kind` (`income` ou `expense`), `amount` como string decimal invariante positiva (sem separador de milhares, expoente ou mais de duas casas), `transactionDate` em `YYYY-MM-DD` e `description`. O recurso devolve esses campos mais `id`, `currency` (`BRL`), `source` (`manual`) e `status` (`active` ou `canceled`). Valores devolvidos têm duas casas. Limites e transições seguem o [modelo de dados](../data-model.md).

No cadastro, a SPA mantém chave e corpo da tentativa em memória para reenvio após resultado incerto. Uma nova tentativa intencional usa nova chave. Em edição ou cancelamento, usa uma única ETag forte do detalhe, com aspas, em `If-Match`. ETag fraca, curinga, múltiplas tags ou valor malformado recebem erro. Após `412`, a SPA pede para recarregar o detalhe antes de uma nova tentativa.

## Filtros e paginação

| Campo | Regra |
|---|---|
| `accountId` | Opcional. Aceita conta ativa ou arquivada do titular. Sem filtro, considera todas as contas do titular. |
| `kind` | Opcional. Aceita `income` ou `expense`. Sem filtro, considera ambas. |
| `from`, `to` | Datas inclusivas em `YYYY-MM-DD`. Fornecer ambas com `from <= to` ou omitir ambas para o mês civil atual em America/Sao_Paulo. |
| `page`, `pageSize` | Inteiros. Página começa em 1 e tem padrão 1. Tamanho padrão 25 e intervalo 1 a 100. Rejeitar deslocamento acima de `Int32.MaxValue`. |

A resposta contém `items`, `page`, `pageSize`, `totalCount`, `from`, `to` e `totals`. A SPA mantém as datas efetivas ao mudar de página e volta à primeira página ao aplicar filtros. A ordem é `transactionDate DESC, id DESC`. O histórico e a contagem incluem cancelamentos. A interface identifica esses itens como **Cancelada**. Páginas além do fim retornam `items: []` com a contagem e os totais do filtro.

`totals` contém `income`, `expense` e `net` como strings decimais invariantes de duas casas. Usa apenas movimentações ativas do mesmo filtro inteiro. O líquido é receitas menos despesas e pode ser negativo. Sem movimentações ativas, os três valores são `"0.00"`. Cada resposta reúne página e totais consistentes para aquela leitura. A navegação entre páginas pode refletir mudanças feitas depois de uma resposta anterior.

## Erros

Formato `application/problem+json` com `status`, `title`, `code` e, quando aplicável, `detail` ou `errors`. Mensagens em português.

| HTTP | Código | Condição |
|---|---|---|
| 400 | `VALIDATION_FAILED` | Campos, natureza, período ou paginação inválidos. |
| 400 | `INVALID_IDEMPOTENCY_KEY` | Chave de cadastro ausente, vazia, inválida ou repetida no cabeçalho. |
| 400 | `INVALID_TRANSACTION_VERSION` | `If-Match` inválido. |
| 401 | `SESSION_REQUIRED` | Sessão ausente ou inválida. |
| 403 | `INVALID_CSRF_TOKEN` | CSRF ausente ou inválido. |
| 404 | `ACCOUNT_NOT_FOUND` | Conta solicitada inexistente ou de outro titular. |
| 404 | `TRANSACTION_NOT_FOUND` | Movimentação inexistente ou de outro titular. |
| 409 | `ACCOUNT_ARCHIVED` | Cadastro ou troca para conta arquivada. |
| 409 | `IDEMPOTENCY_KEY_CONFLICT` | Chave de cadastro já usada com outra entrada normalizada. |
| 409 | `TRANSACTION_CANCELED` | Revisão atual, com tentativa de corrigir ou cancelar uma movimentação já cancelada. |
| 412 | `STALE_TRANSACTION_VERSION` | Revisão diferente da atual. |
| 428 | `TRANSACTION_VERSION_REQUIRED` | `If-Match` ausente em mutação de movimentação existente. |
| 500 | `INTERNAL_ERROR` | Falha inesperada, sem detalhes internos. |
