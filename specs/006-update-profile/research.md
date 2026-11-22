# Pesquisa — 006 · Atualização cadastral

**Data**: 2026-11-22. Revisão após esclarecimento B de conflito por campo. Pesquisa documental e inspeção somente leitura; nenhuma implementação ou validação funcional realizada.

## Questões resolvidas

### R1 — Como atualizar diferenças e rejeitar conflitos no mesmo campo?

- **Decision**: Aplicar o esclarecimento B de 2026-11-22 com PATCH esparso contendo `changes` e `originalValues`, ambos obrigatórios e com exatamente as mesmas chaves dos campos pessoais editados. O cliente mantém o snapshot da consulta inicial, calcula o delta canônico e envia os valores originais correspondentes. O servidor recarrega o Owner sob `UPDLOCK,HOLDLOCK`, compara os originais canônicos com os valores vigentes e rejeita o envio inteiro com `409 PROFILE_UPDATE_CONFLICT` se qualquer campo editado divergir, inclusive quando o valor desejado já coincidir com o vigente. Só depois verifica mudança efetiva, comprova senha, mescla e persiste seletivamente. Campos não editados não participam da comparação. Nenhuma senha integra originalValues.
- **Rationale**: FR-010 e SC-005 agora proíbem sobrescrever uma edição concorrente no mesmo campo e exigem aceitar alterações disjuntas. Bloqueio sozinho serializa gravações, mas não identifica se o formulário ficou desatualizado desde a abertura. O mecanismo combina comparação dos valores originais pela aplicação com seção crítica SQL existente; essa composição é uma decisão de desenho, fundamentada no conceito de comparação com estado original da [documentação de concorrência EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) e no padrão local LockOwnerAsync. Tracking seletivo permanece conforme [Change tracking EF Core](https://learn.microsoft.com/en-us/ef/core/change-tracking/).
- **Alternatives considered**: Última gravação vence contraria o esclarecimento aprovado; token global/rowversion obrigatório rejeitaria edições de campos diferentes e acrescentaria estado desnecessário; PUT com snapshot inteiro sobrescreveria campos intactos; JSON Patch genérico ampliaria operações/dependências. originalValues é referência de edição, nunca identidade/autorização; sessão/CSRF e validação do servidor continuam obrigatórios.
- **Repetition and review**: Repetir exatamente um PATCH já confirmado com originais antigos retorna conflito se o estado vigente mudou, e não unchanged. Sem chave idempotente nova, resposta perdida exige GET e revisão explícita; nunca atualizar silenciosamente originais e reenviar. A UI conserva o rascunho pessoal, consulta os dados atuais por ação do usuário e só substitui a referência após revisão explícita antes de novo submit. Valores canônicos atuais iguais aos originais não permitem detectar mudança intermediária que tenha sido revertida; o critério aprovado é diferença entre valor vigente e carregado, sem histórico/versionamento por campo.

### R2 — Como assegurar atomicidade e e-mail único?

- **Decision**: Transação compartilhada no FinanceManagerDbContext para diferenças do Owner, eventual hash e auditoria. Reutilizar índice `UX_Owners_NormalizedEmailHash`, excluindo o próprio Id na consulta prévia e traduzindo somente violações 2601/2627 do índice esperado. Normalizar conforme RegistrationProfile: trim e ToUpperInvariant para identificação, telefone em dígitos e apelido vazio ausente.
- **Rationale**: O cadastro atual já usa esse índice e transações; chamadas SaveChanges dentro de transação não representam commit final independente. [Transações EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions). Bloqueio por titular protege a leitura seguida de escrita; o índice continua necessário entre titulares distintos. [Hints SQL Server](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table?view=sql-server-ver17).
- **Alternatives considered**: Só consulta prévia permitiria duplicidade sob concorrência; transações separadas para perfil e hash permitiriam estado parcial; recriar o titular romperia os vínculos financeiros.

### R3 — Como comprovar a senha sem gravar uma credencial que não mudou?

- **Decision**: Reutilizar PasswordHasher do Identity com VerifyHashedPassword sob bloqueio; `Success` e `SuccessRehashNeeded` comprovam a senha atual sem rehash automático. Validar nova senha com RegistrationPasswordPolicy e confirmação exata. Se nova senha for ordinalmente igual à atual comprovada, preservar hash e stamps de credencial. Para troca efetiva, gerar hash pelo Identity, renovar SecurityStamp e persistir seletivamente junto ao perfil.
- **Rationale**: No-op não deve alterar hash por política interna de rehash. Verificação de hash retorna um resultado de comprovação. [PasswordHasher.VerifyHashedPassword](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.passwordhasher-1.verifyhashedpassword?view=aspnetcore-10.0). `UserManager.CheckPasswordAsync` pode atualizar hash/stamp em `SuccessRehashNeeded`; essa consequência foi verificada no [código primário de UserManager](https://github.com/dotnet/aspnetcore/blob/v10.0.14/src/Identity/Extensions.Core/src/UserManager.cs).
- **Alternatives considered**: Reimplementar hash é indevido; CheckPasswordAsync/ChangePasswordAsync sem controlar efeitos antecipados dificultam no-op e escrita seletiva. A senha atual não precisa passar pela política nova.

### R4 — Como preservar a proteção de tentativas em atualizações rejeitadas?

- **Decision**: Respeitar LockoutEnabled/IsLockedOutAsync e as opções atuais de cinco falhas/cinco minutos; em falha real de comprovação, usar AccessFailedAsync antes de qualquer atribuição cadastral e confirmar somente os metadados de segurança. Conferir o IdentityResult e não ignorar conflito de escrita. Usar a política HTTP `login` no PATCH (dez pedidos/minuto por IP). Eventual reset de contagem após comprovação correta é bookkeeping de autenticação, isolado das diferenças do perfil.
- **Rationale**: Dar rollback na contagem de erro anularia o bloqueio. AccessFailedAsync incrementa falhas e aplica bloqueio ao atingir o limite. [UserManager.AccessFailedAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1.accessfailedasync?view=aspnetcore-10.0). O adaptador atual de login usa `CheckPasswordSignInAsync(..., lockoutOnFailure: true)`; a nova comprovação deve manter a mesma proteção sem criar sessão.
- **Alternatives considered**: Validar só com hasher não limita tentativas; desfazer todos os metadados com perfil rejeitado elimina proteção; usar login HTTP para comprovar criaria efeitos de sessão indevidos. APIs Identity de contagem podem salvar a entidade inteira; chamá-las somente sem alterações pessoais pendentes e recarregar o Owner para o caminho seletivo.

### R5 — O que acontece com sessões após alteração de credencial?

- **Decision**: Preservar as sessões existentes, a revogação por saída e a inatividade de trinta minutos; não introduzir revogação automática ou novo login ao salvar. Atualizar o e-mail exibido na SPA com o perfil confirmado. Próximos logins usam credenciais vigentes.
- **Rationale**: A spec preserva política de sessão. `OwnerTicketStore` usa esquema `FinanceManager.Owner` e recompõe o ticket consultando OwnerSessions e Owner.Email; não usa SecurityStampValidator. O serviço de conclusão legada revoga sessões explicitamente e, por isso, não é reutilizado para atualizar um cadastro concluído. Esta decisão é inferência do código local e da premissa aprovada, não comportamento presumido do cookie padrão Identity.
- **Alternatives considered**: Revogar todas as sessões mudaria a premissa da spec; reutilizar a transição legada também invalidaria autorizações e estado não relacionados.

### R6 — Reuso das validações e diferença de interação

- **Decision**: Reutilizar RegistrationProfile/RegistrationPasswordPolicy no backend e funções puras pessoais/senha no frontend; extrair somente o necessário de registration-validation.ts. Criar componente de atualização próprio. Máscara e limite do telefone continuam imediatos, mas erros e indicadores de validade são calculados no submit, nunca em input/blur. Mensagens anteriores podem ser limpas durante edição, sem novos resultados.
- **Rationale**: Register hoje apresenta critérios dinâmicos e exige senha sempre; o novo fluxo tem senha opcional e validação apenas no envio. Reutilizar integralmente o template existente alteraria seu contrato ou anteciparia erros.
- **Alternatives considered**: Duplicar regex/política causa divergência; refatorar todas as telas de cadastro extrapola o escopo.

### R7 — Auditoria e migração mínima

- **Decision**: Acrescentar ProfileUpdated em RegistrationAuditAction e na constraint `CK_RegistrationAudits_Action`, com migração nova e snapshot. Um evento por atualização efetiva contém somente OwnerId, Action e UTC; não registrar valores pessoais, senhas ou requisição. Nenhum evento cadastral em no-op ou rejeição.
- **Rationale**: Registra a alteração sensível conforme a constituição usando a estrutura já existente. A constraint atual aceita somente Registered e RegistrationCompleted, exigindo a migração mesmo sem campos novos.
- **Alternatives considered**: Nova tabela/sistema de eventos seria desnecessário; reutilizar ação histórica falsearia o evento; gravar payload exporia dados pessoais. Downgrade com eventos novos deve falhar explicitamente sem remover evidência.

## Fontes locais e conclusão

Inspecionados: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`, `src/frontend/package.json`, `src/frontend/src/main.ts`, `app.routes.ts`, `auth.service.ts`, `registration-form.component.ts`, `registration-validation.ts`; classes RegistrationProfile, Owner, OwnerRegistrationService, SqlOwnerRegistrationStore, RegistrationAudit, FinanceManagerDbContext, IdentityOwnerAdapter, OwnerTicketStore, SqlOwnerSessionStore, SessionAdmissionFilter e OwnerAuthentication. Artefatos aprovados da 004 usados como dependência, sem modificá-los.

Nenhuma dúvida técnica aberta. Locks, rollback, gravação seletiva, metadados de bloqueio e migração precisam de evidência em SQL Server na implementação; pesquisa não demonstra a passagem desses testes.
