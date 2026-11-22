# Modelo de dados — 006 · Atualização cadastral

## Owner existente — AspNetUsers

Nenhuma nova identidade, coluna pessoal ou relação financeira é criada.

| Campo | Regra de atualização |
|---|---|
| Id | Guid estável; obtido exclusivamente da sessão; nunca recebido como autoridade no corpo |
| FullName | Obrigatório; trim externo; preservar grafia; modificar somente se diferente |
| Nickname | Opcional; trim; null/vazio/espaços explicitamente enviados removem; omitido preserva |
| BirthDate | Data civil existente, não futura em America/Sao_Paulo; hoje aceito, sem idade mínima |
| Email | Formato da 004; trim; grafia existente preservada se o NormalizedEmail não mudar |
| NormalizedEmail | ToUpperInvariant do e-mail aparado; único; omissão preserva |
| NormalizedEmailHash | Coluna calculada binary(32) existente, com índice único; não atribuída pelo código |
| PhoneNumber | Exatamente 11 dígitos ASCII após normalização; aceitar dígitos ou máscara completa no transporte |
| PasswordHash | Somente Identity; muda apenas com senha atual comprovada e nova senha efetivamente diferente |
| SecurityStamp | Renovar somente em troca efetiva de senha; não controla revogação no esquema de sessão atual |
| ConcurrencyStamp | Controle Identity existente; renovar em atualização efetiva; no-op cadastral sozinho não renova, mas bookkeeping Identity pode renovar este metadado |
| AccessFailedCount / LockoutEnd / LockoutEnabled | Metadados de proteção existentes, cinco falhas/cinco minutos; erro de comprovação pode atualizá-los sem atualizar perfil |
| UserName / NormalizedUserName / ProvisioningSlot / RegistrationCompletedAtUtc | Preservados; edição cadastral não reinicia registro nem transição |

As invariantes pessoais são as de RegistrationProfile, descritas em FR-005/006 da spec. Não introduzir limites de nome/apelido/e-mail diferentes dos do cadastro. Nova senha e confirmação seguem RegistrationPasswordPolicy e FR-009; senha atual é somente prova da credencial vigente.

## OwnerProfileChanges — valor transitório do domínio

Representa um conjunto restrito de alterações: fullName, nickname, birthDate, email e phoneNumber. Cada campo editado contém presença, valor original e valor desejado; nullable sozinho não distingue omissão de remoção. No transporte, changes e originalValues são objetos esparsos obrigatórios, com exatamente as mesmas chaves; no domínio/aplicação, manter esse significado em tipos explícitos. originalValues contém apenas os valores carregados dos campos pessoais editados, nunca senha, stamp ou identificador. Não persistir comando ou snapshot, nem adicionar versões/colunas por campo.

Antes de descartar deltas equivalentes ou mesclar, o domínio compara os originais canônicos dos campos editados com o perfil vigente: qualquer diferença gera conflito do comando inteiro, mesmo se o valor desejado já for igual ao vigente. Sem conflito, normaliza, compara e mescla o delta sobre o perfil vigente; valida o perfil resultante por RegistrationProfile.Create. A UI faz a comparação adicional com o snapshot inicial para não enviar como editado um campo que apenas ficou desatualizado em outra sessão. Campo editado e depois revertido não compõe o delta. E-mail equivalente por normalização é no-op e conserva grafia anterior. Somente apelido aceita remoção; obrigatório vazio/null é inválido.

## PasswordChange — comando transitório

Quando ausente, não há prova ou alteração de senha. Quando presente, exige currentPassword, newPassword e confirmPassword; nenhuma dessas informações é persistida como texto, retornada, auditada ou registrada em logs. Após comprovar a atual, igualdade ordinal entre atual e nova significa ausência de mudança de credencial, sem rehash. SuccessRehashNeeded na comprovação é sucesso, sem escrita automática.

## Sessão existente — OwnerSessions

Id/OwnerId, CreatedAtUtc, LastActivityAtUtc e RevokedAtUtc mantêm regras existentes. Consulta e escrita usam a sessão autenticada do próprio titular concluído; revalidar validade no envio. Admissão pode atualizar LastActivityAtUtc mesmo quando formulário resulta em erro ou no-op; isso não é regravação do cadastro. Alteração de hash ou e-mail não revoga sessões nesta feature. Saída/revogação/inatividade continuam bloqueando novos acessos.

## Auditoria existente — RegistrationAudits

| Campo | Conteúdo |
|---|---|
| Id | Identificador gerado já existente |
| OwnerId | FK estável do titular atualizado |
| Action | Novo valor ProfileUpdated, além de Registered e RegistrationCompleted existentes |
| OccurredAtUtc | Instante UTC obtido de TimeProvider |

Um evento por commit com diferença efetiva, incluindo troca apenas de senha; zero eventos em rejeição/no-op. Perfil, hash e evento são atômicos. Nunca incluir nomes, e-mail, telefone, datas pessoais, senha ou seus valores anteriores. Não há auditoria textual de payload.

Migração nova `AllowProfileUpdateAudit`: ampliar check de Action, atualizar modelo/snapshot, preservar registros e vínculos existentes. Down deve interromper com mensagem clara se ProfileUpdated já existir, em vez de apagar eventos. Sem modificação de migrações históricas, sem novo índice de e-mail.

## Relações e invariantes preservadas

- Owner 1:N OwnerSession e RegistrationAudit permanecem por OwnerId.
- Contas financeiras, movimentações, obrigações e pagamentos continuam associados ao mesmo OwnerId, sem UPDATE em tabelas financeiras.
- Cadastros de titulares distintos nunca compartilham e-mail normalizado, inclusive durante corrida com register ou conclusão legada.

## Transições

| Estado inicial / envio | Resultado |
|---|---|
| Sem sessão válida ou titular não concluído | Recusa sem divulgar perfil ou alterar cadastro |
| Sessão válida + consulta | Projeção pessoal atual, sem hash/stamps/segredos |
| Delta vazio + nenhuma senha nova efetiva | unchanged; perfil/hash/SecurityStamp/auditoria intactos; ConcurrencyStamp só pode mudar por bookkeeping de segurança necessário |
| Campo editado vigente diferente do original carregado | 409 PROFILE_UPDATE_CONFLICT; rejeitar comando inteiro inclusive senha, sem perfil/hash/evento; solicitar revisão preservando rascunho pessoal |
| Dados inválidos / e-mail ocupado | Erro por campo ou conflito; nada cadastral muda |
| Prova de senha inválida | Perfil/hash/auditoria intactos; contagem de proteção é confirmada, podendo bloquear nova prova |
| Conta com prova bloqueada | 429; não verificar senha, não atualizar cadastro |
| Delta válido + prova válida quando exigida | Atualização seletiva do mesmo Owner + eventual hash + um evento no commit |
| Falha de persistência / conflito | Rollback integral do perfil/hash/evento, sem sucesso |

## Concorrência

Carregar Owner rastreado em transação com UPDLOCK,HOLDLOCK por Id. Antes de qualquer atribuição cadastral, comprovação ou geração de hash/auditoria, comparar TODOS os valores originais enviados com os vigentes segundo as normalizações do domínio: trim no nome, apelido vazio como null, data civil, e-mail pela identidade normalizada e telefone em dígitos. A mesma regra de normalização deve servir ao snapshot, delta e comparação de conflito, sem remover acentos/grafia de nomes.

Se algum original divergir, rejeitar TODO o comando com 409 PROFILE_UPDATE_CONFLICT e errors para os campos em conflito, sem devolver valores nessa mensagem. O valor desejado não elimina conflito: original A, desejado B e vigente B também é rejeitado. Campo editado e revertido ao original é omitido pelo cliente. Campos não editados nunca são incluídos nos objetos nem usados como precondição; deltas disjuntos válidos continuam permitidos. Sem delta pessoal, enviar ambos os objetos vazios; mudanças pessoais em outra sessão não bloqueiam troca isolada de senha, que exige a credencial vigente.

Após comparação sem conflito, mesclar sobre estado vigente e validar perfil completo; nenhum snapshot inteiro é escrito. ConcurrencyStamp continua protegendo escritas externas durante a transação, sem token global obrigatório no cliente. Índice único garante e-mail entre titulares. Ambos os conflitos causam rollback integral de perfil/hash/evento, sem retry automático. Não detectar mudança intermediária já revertida ao mesmo valor canônico: não há histórico/versionamento por campo nesta feature.
