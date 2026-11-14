# Contrato HTTP · 004

Evolui somente autenticação/cadastro dos contratos da 001. API e SPA são atualizadas juntas; o corpo legado `{username,password}` não continua sendo alias do login definitivo. Contratos financeiros das features 001–003 permanecem, com acesso condicionado a titular concluído e sessão válida.

## Regras comuns

- HTTPS em localhost, respostas `/api` com `Cache-Control: no-store`; erros `application/problem+json`, `status`, `code` estável e `title` em português. Nunca devolver senha, hash, token de transição ou dados do titular em erros.
- Todas as mutações, inclusive anônimas, exigem par antiforgery existente e header `X-XSRF-TOKEN`. Falha: `403 INVALID_CSRF_TOKEN`. Obter `GET /api/auth/csrf` antes da primeira mutação e após mudanças de identidade/logout.
- Corpo JSON estrito; nomes de campos desconhecidos rejeitados. Validação retorna `400 VALIDATION_FAILED`, com extensão `errors`: mapa de campo para lista de mensagens em português, sem repetir valores recebidos. Adaptar também a resposta de model binding para campos ausentes/data malformada.
- Não aceitar `OwnerId`, estado de conclusão ou instante de autorização enviados pelo cliente como autoridade. Controllers delegam aos serviços de aplicação.
- Login e prova usam a mesma política de dez tentativas/minuto por IP; cinco falhas bloqueiam o titular por cinco minutos. Responder `429 LOGIN_THROTTLED` tanto no bloqueio quanto na limitação, sem criar sessão/autorização. Credenciais inválidas: mensagem genérica idêntica, por exemplo “Credenciais inválidas.”

## DTO de cadastro

Usado em cadastro inicial e conclusão:

```json
{
  "fullName": "Pessoa de Teste",
  "nickname": null,
  "birthDate": "1990-05-20",
  "email": "pessoa@example.test",
  "phoneNumber": "+55 (11) 90000-0000",
  "password": "SenhaSintetica!42"
}
```

`nickname` pode estar ausente/null/vazio; demais campos são obrigatórios. Nascimento é data ISO civil; não converter por UTC. Aplicar FR-003–007 no servidor, inclusive política Unicode da senha e normalização coerente do e-mail. Exemplos são exclusivamente sintéticos.

## Endpoints

| Método e rota | Entrada e autorização | Resposta |
|---|---|---|
| `GET /api/auth/csrf` | Anônimo, existente. | `204` e cookies antiforgery existentes. |
| `GET /api/auth/registration-state` | Anônimo; consulta somente disponibilidade. | `200 {"state":"empty"}` ou `legacyPending` ou `completed`. Sem ID, e-mail, nome ou telefone. |
| `POST /api/auth/register` | DTO de cadastro + CSRF; somente instalação vazia. | `201 {"message":"Cadastro concluído. Entre para continuar."}` após commit; nenhum cookie financeiro. |
| `POST /api/auth/login` | `{ "email":"pessoa@example.test", "password":"..." }` + CSRF. | `204` e cookie financeiro existente se titular concluído e credenciais válidas. `401 INVALID_CREDENTIALS` caso contrário. |
| `POST /api/auth/registration/proof` | `{ "username":"acesso-antigo", "password":"..." }` + CSRF; titular incompleto. | `200 {"expiresAtUtc":"2026-11-12T15:10:00Z"}` e cookie restrito; nunca cookie/sessão financeira. |
| `GET /api/auth/registration/authorization` | Cookie restrito, sem exigir sessão financeira. | `200 {"expiresAtUtc":"..."}` se válido, sem renovar prazo. `401` se ausente/expirado/revogado ou titular concluído. |
| `POST /api/auth/registration/complete` | DTO de cadastro + cookie restrito + CSRF. | `200 {"message":"Cadastro concluído. Entre para continuar."}` após commit, limpeza do cookie restrito e do cookie financeiro presente; sem login automático. |
| `GET /api/auth/session` | Sessão financeira válida de titular concluído. | `200 {"email":"pessoa@example.test"}`; substitui DTO `username` antigo. `401 SESSION_REQUIRED` caso contrário. |
| `POST /api/auth/logout` | Sessão válida + CSRF. | `204`, revogação/limpeza existentes; `401 SESSION_REQUIRED` se ausente/expirada. |

O formulário **Usar acesso antigo** dentro do login chama `registration/proof`, assim como **Criar conta → Completar cadastro existente**. Não forçar nome legado a passar por validação de e-mail. Depois de concluído, a mesma prova é rejeitada genericamente e nome legado não autentica por nenhuma rota.

## Erros e repetição

| Situação | Status/code e efeito |
|---|---|
| Cadastro inicial quando já concluído, inclusive reenvio após resposta perdida | `409 OWNER_ALREADY_REGISTERED`, orientar login, nenhuma substituição. |
| Cadastro inicial quando existe legado incompleto | `409 REGISTRATION_REQUIRED`, orientar comprovação e conclusão. |
| Prova inválida ou titular inexistente/já concluído | `401 INVALID_CREDENTIALS`, mesma mensagem, sem autorização. |
| Conclusão sem cookie ou token desconhecido | `401 REGISTRATION_AUTHORIZATION_REQUIRED`, solicitar prova. |
| Conclusão com autorização expirada ainda enviada | `401 REGISTRATION_AUTHORIZATION_EXPIRED`, solicitar nova prova; nenhum dado persistido alterado. |
| Emissão atrasada até completar dez minutos desde a comprovação | `401 REGISTRATION_AUTHORIZATION_EXPIRED`, solicitar nova prova, sem entregar cookie utilizável. |
| Conclusão com autorização revogada | `401 REGISTRATION_AUTHORIZATION_REQUIRED`, não reabrir cadastro; atualizar disponibilidade para orientar login. |
| Outra conclusão vence disputa já iniciada com autorização válida | `409 REGISTRATION_ALREADY_COMPLETED`, orientar login, sem sobrescrita. |
| Falha de gravação | Erro em português sem detalhes internos; `500 INTERNAL_ERROR` para falha inesperada; rollback integral e sem confirmação de sucesso. |

Se navegador já removeu cookie pelo vencimento, ausência produz `REGISTRATION_AUTHORIZATION_REQUIRED`; a interface usa seu `expiresAtUtc` para explicar a expiração. Ambos os caminhos preservam os dados não secretos no formulário e exigem prova nova. Cadastro inicial não exige chave idempotente: slot único e estado atual impedem duplicação; reenvio não recebe falso sucesso nem muda dados. Na conclusão, após resposta perdida, estado `completed` orienta novo login; nunca reaplicar a credencial.

## Cookies, tempo e barreira financeira

Sessão financeira mantém `__Host-FinanceManager`, Secure, HttpOnly, SameSite=Lax, sem persistência de navegador e com revogação SQL/inatividade de trinta minutos. Cadastro inicial e prova não emitem esse cookie.

Autorização usa `__Host-FinanceManager-Registration`, Secure, HttpOnly, SameSite=Strict, Path=/, sem Domain. Vencimento SQL e do cookie é `VerifiedAtUtc + dez minutos`; VerifiedAtUtc é capturado no sucesso da comprovação, antes de aguardar bloqueio transacional. Espera, persistência e envio não reiniciam o prazo; se a emissão ocorrer após o limite, exigir nova prova sem entregar cookie utilizável. Token criptográfico opaco; sem principal do esquema financeiro. `GET authorization`, preenchimento e tentativas de conclusão não renovam prazo.

Rotas financeiras recusam sessão incompleta, ausente, revogada ou expirada com `401 SESSION_REQUIRED`; cookie restrito sozinho não autentica. Estado pendente na SPA encaminha à conclusão com prova obrigatória. Sessões anteriores à transição são revogadas no mesmo commit de perfil/hash/conclusão; mesmo um cookie copiado anteriormente não permite acesso posterior. Autorizações de outras abas também deixam de valer. Revalidar estado e autorização dentro da transação evita conclusão/prova concorrentes.

Não alterar leitura/escrita de contas, movimentações, obrigações ou baixas nesta feature além do gate de autenticação. Titularidade continua validada no servidor por identificador estável, nunca pelo e-mail enviado.
