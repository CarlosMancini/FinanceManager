# Contrato HTTP — 006 · Próprio cadastro

**Revisão**: 2026-11-22, esclarecimento B de FR-010/SC-005.

## Convenções

Base `/api/auth/profile`; HTTPS local, JSON camelCase, mensagens em português e erros `application/problem+json` no formato ApiProblems existente. Todos os retornos usam `Cache-Control: no-store`; nenhum eco de valores inválidos/segredos. Datas civis no formato `YYYY-MM-DD`, sem conversão UTC. Endpoints exigem Authorize, cadastro concluído e sessão vigente admitida pelo filtro existente. OwnerId/SessionId vêm dos claims, nunca do cliente.

Não existe rota com Id de titular nem seleção de outro cadastro por query string. Parâmetros de seleção como ownerId, userId, id ou email na URL devem ser rejeitados com 400, sem selecionar outro titular. Membros JSON desconhecidos são rejeitados pela configuração estrita existente; rejeitar também nomes repetidos no objeto de alterações para não aplicar valores ambíguos.

## GET /api/auth/profile

Sem corpo e sem parâmetros. `200 application/json`:

```json
{
  "fullName": "Pessoa de Teste",
  "nickname": null,
  "birthDate": "1990-05-20",
  "email": "pessoa@example.test",
  "phoneNumber": "61998433486"
}
```

Somente os cinco campos pessoais. Nunca retornar hash, senha, stamps, autorização legada ou outros titulares. O cliente guarda esses dados como referência da abertura, sem token global de versão. A consulta não valida senha, não altera perfil e não abre fluxo de conclusão legada. Sem sessão válida: `401 SESSION_REQUIRED`.

## PATCH /api/auth/profile

`Content-Type: application/json`, cookie de sessão e header `X-XSRF-TOKEN`, obtido pelo fluxo atual `GET /api/auth/csrf`. Política de rate limit `login` no endpoint de escrita, inclusive quando não há troca de senha. Não expor endpoint separado de comprovação.

Formato estrito:

```json
{
  "changes": {
    "phoneNumber": "61912345678",
    "nickname": null
  },
  "originalValues": {
    "phoneNumber": "61998433486",
    "nickname": "Apelido anterior"
  }
}
```

- `changes` e `originalValues` são objetos obrigatórios, podendo ser ambos vazios, com EXATAMENTE as mesmas chaves. Objeto ausente/null, arrays, membros duplicados, chaves faltantes/excedentes ou fora da allowlist são inválidos.
- Allowlist de ambos os objetos: fullName (string), nickname (string ou null), birthDate (string de data civil), email (string), phoneNumber (string).
- Campos omitidos preservam o valor vigente. nickname null/vazio/espaços remove; null/vazio em campo obrigatório é inválido, não significa preservar.
- O frontend inclui em changes somente diferenças canônicas contra o snapshot inicial e em originalValues os valores carregados das mesmas chaves editadas. Campos não editados não integram nenhum objeto nem são precondição; originalValues não autoriza acesso e nunca inclui senha, hash, stamp ou identificador.
- No servidor, normalizar originais/vigentes por trim no nome/apelido (apelido vazio = null), data civil, telefone em dígitos e identidade de e-mail por trim+ToUpperInvariant. Cobrir paridade com a apresentação inclusive Unicode, sem restringir formatos já aceitos pelo cadastro.
- Telefone aceita exatamente 11 dígitos ASCII ou máscara completa `(DD) XXXXX-XXXX`; retorna sempre os dígitos.
- `passwordChange` pode ser omitido; null explícito é inválido. Quando presente, é objeto e seus três campos são obrigatórios, strings, sem trim/normalização.

Exemplo de troca junto a dados pessoais:

```json
{
  "changes": { "email": "novo@example.test" },
  "originalValues": { "email": "pessoa@example.test" },
  "passwordChange": {
    "currentPassword": "Exemplo-Atual!1234",
    "newPassword": "Exemplo-Novo!5678",
    "confirmPassword": "Exemplo-Novo!5678"
  }
}
```

Senhas do exemplo são sintéticas, exclusivamente para documentar o contrato. Nunca reutilizá-las em produção ou registrar esse corpo em logs. Apenas troca de senha envia `changes: {}` e `originalValues: {}`, sem precondição sobre campo pessoal não editado; a senha vigente continua obrigatória na comprovação. Não incluir OwnerId, UserName, estado de registro, hash, stamp ou flags para desativar proteções.

### Comparação de conflito e atomicidade

Após validação da forma/valores, sob o mesmo bloqueio e transação do Owner:

1. Comparar TODOS os valores originais canônicos enviados com o estado vigente, somente nas chaves editadas.
2. Se qualquer um divergir, retornar `409 PROFILE_UPDATE_CONFLICT` com errors dos campos conflitantes e rejeitar TODO o comando, inclusive eventual senha, outros campos e evento de auditoria. Não comprovar senha nem gerar hash/evento nesse ramo; admissão da sessão mantém comportamento existente.
3. Somente sem conflito, avaliar equivalência/no-op, validar perfil mesclado e eventual senha, comprovar a atual e gravar diferenças efetivas/credencial/auditoria como uma operação.

Comparar originais ANTES de eliminar desired==current. Exemplo: original A, desejado B, vigente B também é conflito. Campos não editados que mudaram em outra sessão não impedem o envio. Se o valor canônico vigente voltou ao original após mudança intermediária, não há conflito detectável por este contrato: não existe versionamento histórico por campo.

### Sucesso e no-op

`200`, somente depois do commit, ou após comprovar ausência de diferenças:

```json
{
  "outcome": "updated",
  "message": "Cadastro atualizado com sucesso.",
  "profile": {
    "fullName": "Pessoa de Teste",
    "nickname": null,
    "birthDate": "1990-05-20",
    "email": "novo@example.test",
    "phoneNumber": "61912345678"
  }
}
```

Sem mudanças efetivas e após comparar os originais sem conflito: mesmo formato com `outcome: "unchanged"`, `message: "Não há alterações para atualizar."` e perfil vigente. Não regravar perfil/hash/stamps nem criar evento nesse caso. Prova válida com nova senha igual à atual não gera alteração de credencial; demais diferenças ainda podem ser salvas. Exemplo simples de no-op: changes e originalValues vazios, sem passwordChange. Não converter comando com original obsoleto em unchanged só porque o valor pretendido já coincide com o vigente. Resposta nunca devolve passwordChange ou suas propriedades.

Sessão permanece vigente pelas regras atuais; não emitir login novo nem revogar sessões como consequência automática. A admissão da sessão e metadados de proteção podem ser atualizados independentemente de no-op/rejeição cadastral; isso inclui ConcurrencyStamp se uma API Identity o renovar como parte do bookkeeping, preservando PasswordHash/SecurityStamp em no-op.

### Erros

| Situação | HTTP / code | Efeito e apresentação |
|---|---|---|
| Campo pessoal inválido, corpo inválido ou senha ausente/inválida/divergente | 400 VALIDATION_FAILED | errors por campo; nada cadastral é salvo |
| Senha atual incorreta | 400 VALIDATION_FAILED | errors.currentPassword; persistir contagem de falha, sem encerrar sessão válida nem alterar perfil/hash |
| E-mail de outro titular, inclusive conflito de índice | 409 EMAIL_ALREADY_REGISTERED | errors.email; “Este e-mail já está registrado. Informe outro e-mail.”; rollback integral |
| Original de campo editado diferente do vigente | 409 PROFILE_UPDATE_CONFLICT | errors nas chaves conflitantes, sem valores; “O cadastro mudou durante a atualização. Revise os dados atuais e tente novamente.”; rejeitar TODO o envio, inclusive senha |
| ConcurrencyStamp/deadlock ou disputa de persistência que impeça concluir | 409 PROFILE_UPDATE_CONFLICT | Mesma orientação, errors.profile se não houver campo identificável; rollback, sem retry automático |
| Sem sessão válida / cadastro incompleto | 401 SESSION_REQUIRED | “Entre para continuar.”; nenhuma informação pessoal |
| CSRF inválido/ausente | 403 INVALID_CSRF_TOKEN | Mensagem e fluxo existentes; nenhuma atualização |
| Lockout ou limite por IP | 429 LOGIN_THROTTLED | “Muitas tentativas. Tente novamente mais tarde.”; nada cadastral muda |
| Falha inesperada de gravação | 500 INTERNAL_ERROR | “Não foi possível atualizar o cadastro. Tente novamente.”; rollback, sem detalhes internos |

No conflito, indicar os campos em errors sem ecoar valores originais/atuais/desejados; a revisão obtém os atuais por nova consulta autenticada. Exemplo: `errors: {"phoneNumber": ["Este campo mudou desde a abertura. Revise o valor atual."]}`.

Chaves estáveis de errors: fullName, nickname, birthDate, email, phoneNumber, currentPassword, newPassword, confirmPassword; erros de estrutura usam profile. Para credenciais aninhadas, mapear erros de entrada a essas chaves, sem ecoar valores. Campos inválidos podem ser informados no mesmo envio; não é obrigatório comprovar a senha enquanto o comando tiver erro estrutural, dados inválidos, originais em conflito ou nova senha inválida.

O quinto erro de comprovação que inicia o bloqueio e tentativas durante bloqueio retornam 429; falhas anteriores retornam 400. AccessFailedCount/LockoutEnd persistem mesmo na rejeição. Um erro desse mecanismo nunca é convertido em sucesso cadastral. Não fazer gravação de perfil antes dessa decisão.

### Repetição e concorrência

Não há chave idempotente nova. Replay exato de PATCH pessoal já aplicado com original antigo retorna 409 se vigente divergir do original, MESMO que desejado seja igual ao vigente. Resposta perdida não autoriza inferir sucesso: consultar e revisar explicitamente o perfil; para senha, comprovar credencial vigente em nova tentativa. Não renovar originais nem repetir PATCH automaticamente.

A UI conserva snapshot/rascunho antigos no conflito. A ação de consulta guarda atuais separadamente; somente a decisão explícita de reaplicar/descartar os valores do rascunho redefine a referência e recomputa changes/originalValues, seguida de novo submit manual. Credenciais não são reaplicadas automaticamente: limpar no início da revisão e exigir prova vigente na próxima tentativa de troca.

Atualizações de campos distintos no mesmo titular preservam ambas, mediante recarga sob bloqueio. No mesmo campo com original obsoleto, o segundo envio inteiro é rejeitado; também quando desired==current. Duas trocas com senha atual antiga não podem ambas substituir uma troca já confirmada. Conflito entre titulares por e-mail permite no máximo um vencedor, com rollback do outro inclusive quando também tentou trocar senha.

## Rastreabilidade

GET: FR-002/014. PATCH/presença/originais/conflito/no-op: FR-004–007/010 e SC-005 (esclarecimento B). Senha: FR-003/008–009. Commit, erros e auditoria: FR-011–012. Identidade e sessão: FR-013–014. Ver [matriz do plano](../plan.md#matriz-de-verificação) e [modelo](../data-model.md).
