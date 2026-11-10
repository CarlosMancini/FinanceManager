# Contrato HTTP · 001 · Fundação financeira

API JSON na mesma origem HTTPS da SPA. Requisições do Angular usam caminhos relativos a partir da raiz (`/api/…`). Respostas com sessão ou dados de conta usam `Cache-Control: no-store`. O servidor obtém o titular da sessão e aceita somente os campos documentados.

## Sessão e antiforgery

| Operação | Entrada | Resposta |
|---|---|---|
| `GET /api/auth/csrf` | Sessão opcional. | `204`. Emite token de requisição em `XSRF-TOKEN`, legível pelo Angular, vinculado ao cookie antiforgery protegido e à identidade atual. |
| `POST /api/auth/login` | `{ "username": "...", "password": "..." }` e token CSRF. | `204` e cookie de sessão seguro, HttpOnly, SameSite=Lax, sem persistência de navegador. `401` para credenciais rejeitadas e `429` para bloqueio temporário. |
| `GET /api/auth/session` | Sessão válida. Usado ao carregar a SPA, sem polling. | `200` com `{ "username": "..." }` ou `401`. |
| `POST /api/auth/logout` | Sessão válida e token CSRF. | `204`, revoga a sessão e limpa seu cookie. Sessão ausente ou expirada recebe `401`. |

Todas as mutações, inclusive login e logout, exigem `X-XSRF-TOKEN`. A SPA obtém o token antes da primeira mutação e após login ou logout, para refletir a identidade atual. O cookie antiforgery interno é Secure e HttpOnly. Login é limitado com a política de bloqueio do Identity e limitação por origem de conexão na API. A API responde com status HTTP e a SPA controla a navegação.

## Contas

| Operação | Entrada | Sucesso |
|---|---|---|
| `GET /api/accounts` | Sessão válida. Filtro opcional `status=active\|archived`. Sem filtro, retorna os dois estados. | `200`, lista JSON. Lista vazia é `[]`. |
| `POST /api/accounts` | Sessão, CSRF e `{ "name": "...", "type": "checking" }`. | `201`, corpo da conta, `Location` e `ETag`. |
| `GET /api/accounts/{id}` | Sessão válida. | `200`, corpo da conta e `ETag`. |
| `PUT /api/accounts/{id}` | Sessão, CSRF, `If-Match` e os campos `name` e `type`. | `200`, corpo da conta e `ETag` da versão devolvida. |
| `PUT /api/accounts/{id}/status` | Sessão, CSRF, `If-Match` e `{ "status": "archived" }` ou `{ "status": "active" }`. | `200`, corpo da conta e `ETag` da versão devolvida. |

A conta contém `id`, `name`, `type`, `currency` e `status`. Tipos, estados e validações seguem o [modelo de dados](../data-model.md). A SPA lê a conta individual ao abrir a edição ou confirmar uma mudança de estado. Envia em `If-Match` a única ETag forte recebida, incluindo aspas. A API recusa curingas, tags fracas, múltiplas tags e valores malformados. Ao receber `412`, a SPA pede ao titular que recarregue os dados e evita reenvio automático.

## Erros

Formato `application/problem+json` com `status`, `title`, `code` estável e `detail` quando útil. Os textos ao usuário são em português. Erros de campos podem incluir `errors` com mensagens por campo.

| Status | Código | Condição |
|---|---|---|
| `400` | `VALIDATION_FAILED` | Entrada ou filtro inválido. |
| `400` | `INVALID_ACCOUNT_VERSION` | `If-Match` em formato não aceito. |
| `401` | `SESSION_REQUIRED` | Sessão ausente, inválida, expirada ou revogada. |
| `401` | `INVALID_CREDENTIALS` | Credenciais rejeitadas. |
| `403` | `INVALID_CSRF_TOKEN` | Token antiforgery ausente ou inválido em mutação. |
| `404` | `ACCOUNT_NOT_FOUND` | Conta inexistente ou fora da titularidade da sessão. |
| `409` | `ACCOUNT_NAME_CONFLICT` | Nome já reservado por outra conta do mesmo titular. |
| `412` | `STALE_ACCOUNT_VERSION` | A revisão mudou desde a leitura ou durante o salvamento. |
| `428` | `ACCOUNT_VERSION_REQUIRED` | `If-Match` ausente em mutação de conta existente. |
| `429` | `LOGIN_THROTTLED` | Limite de tentativas atingido. |
| `500` | `INTERNAL_ERROR` | Falha inesperada, com mensagem genérica e sem detalhes de infraestrutura. |
