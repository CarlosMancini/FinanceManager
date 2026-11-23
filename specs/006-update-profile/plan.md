# Implementation Plan: Atualização cadastral do usuário autenticado

**Branch**: `main` | **Date**: 2026-11-22 | **Spec**: [spec.md](spec.md)

**Input**: `specs/006-update-profile/spec.md`, incluindo esclarecimento B de 2026-11-22 (FR-010/SC-005), requisitos FR-001–014, três histórias P1 e checklist 16/16. Solicitação explícita de planejamento em 2026-11-22 autoriza esta fase; implementação depende de tarefas e comando próprios. O setup retornou o identificador lógico `006-update-profile` como BRANCH; `git branch --show-current` confirmou a branch física `main`, que não foi alterada.

## Summary

Adicionar `/profile` à navegação autenticada e expor consulta/atualização do próprio cadastro. O cliente compara o formulário ao snapshot carregado e envia PATCH esparso com changes e originalValues contendo as mesmas chaves editadas; sob bloqueio SQL, o servidor compara os originais canônicos com o estado vigente antes de mesclar. Divergência em qualquer campo editado rejeita o comando inteiro, inclusive a senha; alterações em campos disjuntos continuam permitidas. Sem conflito, mescla somente o delta e valida pelas regras existentes de cadastro. Perfil, eventual novo hash e auditoria são confirmados na mesma transação, sem alterar identidade ou finanças. A troca opcional exige comprovação da senha vigente apenas no envio. Preservar sessões atuais e proteções de autenticação, inclusive contadores de falha quando a atualização for rejeitada.

## Technical Context

**Language/Version**: C# 14 / .NET 10, SDK 10.0.403 em `global.json`; TypeScript 6.0.3 e Angular 22.2.5; Node 24.22.2 conforme `src/frontend/package.json`.

**Primary Dependencies**: ASP.NET Core Identity, EF Core SQL Server e ferramentas EF 10.0.14; RxJS 7.8.2; versões em `Directory.Packages.props`, manifests e lockfiles existentes. Nenhuma dependência nova.

**Storage**: SQL Server existente; reutilizar `AspNetUsers`, `OwnerSessions`, `RegistrationAudits` e índice `UX_Owners_NormalizedEmailHash`. Nova migração apenas para permitir `ProfileUpdated` na restrição de ações da auditoria; sem novos campos pessoais, tabelas ou índices.

**Testing**: xUnit v3 / Microsoft.Testing.Platform, `xunit.v3.mtp-v2` 4.0.3; integração com SQL Server real; Angular/Vitest 4.0.8 e happy-dom 21.0.4; inspeção HTTP e navegador integrado na validação futura.

**Target Platform**: Instalação local de múltiplos titulares em loopback por HTTPS, no ambiente Windows/SQL Server e no empacotamento existente. Nenhuma alteração no instalador ou na exposição de rede.

**Project Type**: Aplicação web em monólito modular, MVC e DDD, com frontend Angular e backend dividido em Domain, Application, Infrastructure e Api.

**Performance Goals**: Não há novo SLA de latência na spec. Carregar somente o perfil e manter curta a transação por titular; nenhum carregamento de histórico financeiro. SC-006 mede conclusão do fluxo por pessoas, sem substituir esse aceite por benchmark técnico.

**Constraints**: Validações somente no envio; isolamento por titular; delta efetivo; precondição por campo editado, sem versão global obrigatória; atomicidade; senha não recuperável; data civil em America/Sao_Paulo via TimeProvider; auditoria UTC; sem dados pessoais/segredos em logs. Preservar cinco falhas/cinco minutos e limitador de dez tentativas/minuto por IP existentes.

**Scale/Scope**: Uma tela, duas operações HTTP autenticadas e manutenção do mesmo titular; todas as histórias P1 integram a entrega. Sem recuperação de senha, administração de terceiros ou mudanças financeiras.

## Constitution Check

*GATE: avaliação anterior à pesquisa e reavaliação após o desenho.*

| Princípio | Gate anterior à pesquisa | Evidência do desenho / gate posterior |
|---|---|---|
| I. Especificação | PASS: spec 006 identificada, checklist completo, planejamento solicitado | FR/SC e esclarecimento B rastreados; spec não alterada; atualização das tarefas e implementação ficam para fases próprias |
| II. Arquitetura | PASS: manter stack, monólito, DDD/MVC e regras no domínio | Controller chama OwnerProfileService; porta IOwnerProfileStore; delta e regras reutilizam domínio; SQL/Identity apenas em Infrastructure |
| III. Integridade | PASS: sem mudança de regra financeira | Mesmo Owner.Id/FKs; comparações de histórico e teste de rollback; data via relógio injetável, auditoria UTC |
| IV. Segurança | PASS: somente próprio titular, HTTPS, CSRF, unicidade | Authorize + SessionAdmissionFilter, identidade do principal, índice existente, lockout e ausência de payload em logs |
| V. Integrações | PASS: sem nova integração | Nenhum provedor ou dado bancário novo |
| VI. Testes | PASS: regras/casos de uso exigem xUnit, SQL e UI | Matriz cobre rejeição de conflito por campo e de troca combinada, edições disjuntas, replay, limites, acesso, migração e usabilidade |
| Convenções | PASS: nomes técnicos em inglês, mensagens em português | Arquivos/contratos definidos abaixo; erros sem conteúdo pessoal |

Reavaliação pós-desenho: PASS em todos os gates, sem exceções constitucionais ou esclarecimentos pendentes. O TODO histórico de ratificação da constituição não é alterado por esta feature. Aprovação documental não é evidência de aceite funcional.

## Project Structure

### Documentation (this feature)

```text
specs/006-update-profile/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── checklists/requirements.md
└── contracts/
    ├── api.md
    └── ui.md
```

`tasks.md` já existe e está desatualizado após o esclarecimento B. Deve ser revisado por `$speckit-tasks` após este plano; esta execução não altera tarefas nem marca implementação como autorizada.

### Source Code (repository root)

```text
src/backend/
├── FinanceManager.Domain/Authentication/
│   ├── RegistrationProfile.cs               # regras existentes reutilizadas
│   ├── RegistrationPasswordPolicy.cs        # regras existentes reutilizadas
│   └── OwnerProfileChanges.cs               # originais por campo, conflito, merge e diferenças
├── FinanceManager.Application/Authentication/
│   ├── OwnerProfileService.cs               # consulta e coordenação da atualização
│   ├── IOwnerProfileStore.cs                # porta de execução atômica
│   └── OwnerProfileResults.cs               # perfil público e resultados tipados
├── FinanceManager.Infrastructure/
│   ├── Persistence/SqlOwnerProfileStore.cs  # locks, Identity, persistência seletiva
│   ├── Persistence/RegistrationAudit.cs     # acrescentar ação ProfileUpdated
│   ├── Persistence/FinanceManagerDbContext.cs
│   ├── Persistence/Migrations/             # migração nova e snapshot
│   └── DependencyInjection.cs
└── FinanceManager.Api/
    ├── Controllers/OwnerProfileController.cs
    └── Controllers/OwnerProfileRequests.cs
src/frontend/src/
├── main.ts                                 # link na navegação existente
└── app/
    ├── app.routes.ts
    └── auth/
        ├── owner-profile.component.ts
        ├── owner-profile.service.ts
        ├── registration-validation.ts      # reuso direcionado das regras puras
        └── auth.service.ts                 # tratamento de 401 e atualização do e-mail
 tests/
 ├── FinanceManager.UnitTests/Authentication/
 └── FinanceManager.IntegrationTests/Authentication/
```

**Structure Decision**: Evoluir o módulo de autenticação existente, com porta própria para a atualização de titular concluído, sem reaproveitar o caso de uso de conclusão legada, que revoga sessões. Componentes e serviços Angular novos recebem seus testes junto aos arquivos existentes. Extrair apenas funções de validação pessoal/senha necessárias ao reuso, com regressão de register e conclusão legada; evitar refatorar o formulário de cadastro para acomodar outra interação.

## Phase 0 — Pesquisa e decisões

Pesquisa concluída em [research.md](research.md), com inspeção do código e documentação primária. Decisões resolvidas: PATCH com presença explícita e originalValues; comparação de originais por campo e merge sob bloqueio; persistência seletiva; hash/lockout sem rehash involuntário; sessões próprias preservadas; auditoria mínima com migração; reuso de regras sem validação antecipada. Nenhum NEEDS CLARIFICATION permanece.

## Phase 1 — Desenho

### Fluxo e limites de responsabilidade

1. Controller obtém OwnerId e SessionId somente dos claims já admitidos e chama o serviço de aplicação. Não recebe DbContext nem escolhe titular a partir do corpo/URL. Consulta retorna projeção pessoal sem segredos e com `Cache-Control: no-store`.
2. OwnerProfileService verifica a forma do comando, presença dos campos de senha e regras puras aplicáveis; calcula o dia civil com TimeProvider. Encaminha à porta o delta tipado, os originais dos mesmos campos e a troca opcional. Validar igualdade exata das chaves de changes/originalValues, inclusive quando ambos vazios; omissão difere de vazio. Referência de edição não é autorização e não contém credencial.
3. O adaptador abre transação, carrega Owner atualizado com `UPDLOCK,HOLDLOCK` pelo Id confiável e confirma cadastro concluído e sessão vigente do mesmo titular. Antes de descartar valores desejados já iguais aos atuais, comprovar senha ou atribuir perfil, o domínio compara TODOS os originais canônicos enviados com os campos vigentes. Divergência retorna 409 PROFILE_UPDATE_CONFLICT com indicação dos campos, sem valores pessoais, e rejeita todo o envio. Campos omitidos não são comparados. Somente sem conflito mescla o delta sobre o perfil atual por RegistrationProfile.Create. A validação do perfil resultante usa o dia avaliado no envio; nenhuma regra é duplicada em SQL/controller.
4. Campos pessoais e novas senhas inválidos retornam erros sem atribuir propriedades ao Owner. Com tentativa de troca válida na forma, o adaptador verifica bloqueio e comprova senha atual com PasswordHasher.VerifyHashedPassword, sem rehash automático. Senha atual inválida incrementa o contador com os mecanismos Identity existentes antes de qualquer atribuição cadastral. Confirmar somente metadados de proteção nesse ramo e encerrar com erro; jamais desfazer a contagem junto com rollback de perfil. Esse bookkeeping de segurança e a admissão da sessão não são alterações cadastrais.
5. Com senha atual válida, considerar senha efetivamente nova apenas se diferir ordinalmente da atual comprovada. Se equivalente, não gerar hash/stamp. Comparar valores canônicos dos campos pessoais com os atuais; e-mail equivalente pelo normalizado conserva a grafia já persistida. Sem diferenças, retornar `unchanged` sem auditoria ou escrita de perfil/credencial. Alterações de ConcurrencyStamp decorrentes exclusivamente do bookkeeping Identity não representam mudança cadastral; em no-op sem bookkeeping necessário, todos os stamps permanecem iguais.
6. Para diferenças efetivas, pesquisar e-mail normalizado excluindo o próprio Id e conservar o índice único como garantia concorrente. Atribuir apenas propriedades alteradas. Alterações exclusivamente cadastrais preservam PasswordHash e SecurityStamp e renovam ConcurrencyStamp. Troca efetiva de senha atualiza PasswordHash pelo hasher Identity somente após todas as validações e renova SecurityStamp e ConcurrencyStamp. Sem alterações efetivas, nenhum desses campos muda por atualização cadastral; ConcurrencyStamp pode mudar exclusivamente pelo bookkeeping de proteção do Identity descrito no item anterior. Não usar DbContext.Update nem UserManager.UpdateAsync para persistir o perfil, pois podem marcar colunas intactas.
7. Persistir com tracking seletivo e auditoria `ProfileUpdated` no mesmo SaveChanges/commit, sem valores pessoais no evento. Reset de contagem após comprovação correta, quando aplicável, permanece metadado de segurança; executar APIs de bookkeeping sem modificações cadastrais pendentes e recarregar a entidade sob o bloqueio antes da persistência seletiva. Falhas inesperadas, conflito de versão, deadlock ou unicidade desfazem todas as alterações cadastrais e de credencial; não repetir automaticamente uma troca de senha.
8. Responder somente após commit com perfil confirmado. SPA substitui snapshot, limpa senhas/recolhe a seção e atualiza o e-mail exibido na sessão. Preservar a sessão; o ticket SQL existente consulta o e-mail vigente a cada reconstrução, sem SecurityStampValidator nesse esquema.

### Concorrência e limites

PATCH contém changes e originalValues com exatamente as mesmas chaves editadas. O snapshot da abertura permanece em memória e não é substituído silenciosamente por consulta posterior. Comparação no domínio: nome/apelido com trim (apelido vazio = null), data civil, telefone em dígitos e e-mail pela identidade normalizada. Normalização da apresentação deve ter testes de paridade com o domínio, incluindo Unicode; o servidor continua autoridade, sem restringir o cadastro existente a ASCII.

Dentro da transação sob lock, qualquer original diferente do vigente rejeita TODO o comando antes de prova de senha ou gravação. Também há conflito em original A / desejado B / vigente B: desired==current não autoriza unchanged antes da precondição. Deltas pessoais disjuntos são aceitos e preservam ambas as mudanças. Objetos vazios não impõem precondições sobre perfil para troca isolada de senha. Não exigir rowversion/ConcurrencyStamp do GET, pois uma versão global rejeitaria mudanças de campos independentes. O ConcurrencyStamp existente protege escritas externas durante a transação, sem substituir a comparação por campo. Mudança intermediária revertida ao valor canônico original não é detectada; a spec compara valores vigentes, sem exigir histórico por campo.

A comprovação da senha continua na mesma seção crítica da atualização: se outra troca já venceu, senha antiga falha. E-mail entre titulares continua garantido pelo índice existente. Em conflito, a UI conserva snapshot e rascunho pessoal, consulta dados atuais por ação explícita e os apresenta para revisão. Só depois da decisão explícita de reaplicar/descartar os dados do rascunho atualiza a referência e recalcula o delta; nenhum rebase ou PATCH automático. Nova tentativa de troca exige a senha vigente, sem transportar segredos pelo fluxo de revisão.

Sem chave idempotente nova, replay de PATCH já gravado com originais antigos retorna conflito quando o estado vigente difere do original, mesmo se os valores pretendidos já estiverem salvos. Após resposta perdida, consultar e revisar; nunca inferir sucesso, atualizar originais e repetir silenciosamente.

### Persistência e migração

Modelo detalhado em [data-model.md](data-model.md). Ampliar enum e check `CK_RegistrationAudits_Action` com `ProfileUpdated` por migração nova, preservando ações históricas, Ids, hashes e FKs. Migração não reprocessa perfis nem recria titulares. Nenhuma tabela financeira é alterada. Testar migração desde a última versão e base vazia em SQL Server; verificar que novas ações são aceitas e ações desconhecidas continuam rejeitadas. Downgrade não pode apagar auditoria nova para satisfazer a restrição antiga; interromper com orientação se existirem ações incompatíveis.

### Contratos e apresentação

Ver contratos [HTTP](contracts/api.md) e [UI](contracts/ui.md). `/profile` exige somente `requireSession`, sem guards de existência de conta. PATCH reutiliza CSRF e política `login`; aplicar o limite ao endpoint de escrita evita expor prova de senha sem proteção. Resposta de senha atual incorreta é 400 por campo, não 401, para não encerrar sessão válida; somente 401 de sessão aciona expiração no interceptor. Instruções de senha estáticas, erros de validação apenas ao submeter, sem indicadores dinâmicos.

## Matriz de verificação

Todos os testes abaixo são trabalho futuro, a detalhar em tasks; nada foi executado como validação funcional nesta fase.

| Cobertura | Evidências requeridas | Requisitos / sucesso |
|---|---|---|
| Domínio xUnit | Ausente vs vazio; apelido removido; delta revertido/canônico; originais e chaves correspondentes; conflito antes de no-op/merge; paridade de normalização Unicode; nome com acento; nascimento hoje/futuro/inexistente; e-mail inválido/.com/.com.br/próprio; telefone 10/11/12 dígitos | FR-005–007/010; SC-002/003 |
| Senha xUnit | Atual obrigatória só na troca; nova/confirmação 11/12/128/129 pontos Unicode, categorias ausentes, espaços, divergência e comparação ordinal; senha atual legada não sofre política nova | FR-003–004/008–009; SC-003/004 |
| Aplicação xUnit | Consulta limitada ao titular; coordenação, relógio, resultados de erros; nenhum save no no-op; sem credencial no resultado; falha/duplicidade sem sucesso | FR-002/004–014; SC-002–005 |
| SQL Server | Delta seletivo confirmado por ChangeTracker/interceptor de teste, não por logging de payload; no-op sem bookkeeping mantém hash/stamps/auditoria; com bookkeeping, hash/SecurityStamp/auditoria permanecem e só metadados de proteção podem mudar; rollback perfil+hash+auditoria; índice duplicado incluindo concorrência com register; updates disjuntos preservados; mesmo campo rejeita envio inteiro inclusive senha (também desired==current); replay com original antigo; troca de senha disputada; conflito de stamp | FR-006/008/010–014; SC-002–005 |
| Lockout e sessão SQL/HTTP | Cinco falhas bloqueiam por cinco minutos, dez pedidos/minuto por IP; contador permanece após rejeição; senha correta não ultrapassa bloqueio; sessões existentes permanecem; logout/expiração ainda negam acesso | FR-008/014; SC-003/005 |
| Migração SQL | Base vazia e upgrade da versão atual, novos eventos aceitos, check antigo ampliado sem perda de dados; downgrade protegido; histórico e titulares preservados | FR-011/013; SC-002/005 |
| Contrato HTTP | GET/PATCH, allowlist e igualdade de chaves changes/originalValues, omissão/null/original ausente ou excedente, corpo malformado, CSRF, códigos, cache, falhas, ID externo rejeitado, dois titulares, ausência de sessão e nenhum segredo em respostas/logs | FR-002/005–014; SC-002–005 |
| Angular | Link/rota com zero contas; loading/falha; prefill; máscaras; opção revela três campos; sem validação em input/blur; erros só ao enviar; snapshot/delta/originais/no-op; 409 preserva rascunho e referência até revisão/reaplicação explícita; descarte de segredo; 401/403/409/429/500 | FR-001–012/014; SC-001–003/005 |
| Regressão | Cadastro/conclusão 004, login por e-mail, guards e isolamento 001–003; comparar OwnerId, quantidades, FKs e valores de contas/movimentações/obrigações/pagamentos antes/depois | FR-005–006/013–014; SC-002/004/005 |
| Navegador integrado e participantes | Fluxo teclado/tela estreita/status acessíveis, recarga após sucesso, login novo e antigo, avaliação de cinco pessoas com pelo menos quatro concluindo em cinco minutos sem assistência | FR-001–014; SC-001–006 |

Guia executável e registro das evidências em [quickstart.md](quickstart.md). Aprovação de SC-006 requer avaliação humana; teste automatizado não a substitui.


### Impacto nas tarefas existentes — revisão necessária antes de implementar

Esta fase preserva tasks.md. A próxima execução de `$speckit-tasks` deve alinhar pelo menos: T003/T004 (comparação original-vigente e normalização), T005/T006 (comando/porta e objetos com mesmas chaves), T011–T013 (contratos/persistência/cliente), T015/T018–T021 (precondição, snapshot, delta e no-op), T024/T027 (conflito antes da troca), T033/T034/T036 (rollback e conflito no mesmo campo), T037/T039/T042 (revisão explícita, replay e validação). T034 ainda prevê última escrita vencendo e precisa substituí-la por rejeição integral. Essa lista não executa nem aprova tarefas; o conteúdo antigo é histórico até sua revisão.
## Complexity Tracking

Nenhuma violação constitucional ou complexidade excepcional. A migração limitada da auditoria é necessária ao registro da alteração sensível; não se cria sistema paralelo de autenticação ou persistência.
