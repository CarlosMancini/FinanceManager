# Validação — 006 · Atualização cadastral

## Preparação inicial — 2026-11-22

- Checklist `requirements.md`: 16/16 concluídos.
- SDK .NET: 10.0.403, corresponde a `global.json`.
- Node observado: 24.22.3; `src/frontend/package.json` fixa 24.22.2.
- SQL Server: `sqlcmd` e `SqlLocalDB` disponíveis; `SqlLocalDB info` falhou ao acessar a configuração de instâncias no registro. Não havia `ConnectionStrings__FinanceManagerTests` nem `ConnectionStrings__FinanceManager` no ambiente inspecionado; nenhuma conexão SQL real foi validada.
- HTTPS: `dotnet dev-certs https --check` informou ausência de certificado válido.
- Baseline Git: alterações preexistentes em `scripts/container/ContainerOperations.ps1`, `Install.ps1`, `Launch.ps1`, `Update.ps1`; diretórios não rastreados `sites/` e `specs/006-update-profile/`.
- Clarificação B e contrato `changes`/`originalValues` identificados. Nenhum teste funcional executado. T001 permanece pendente até resolver e verificar os pré-requisitos.

## Revalidação da preparação — 2026-11-22

- Fora do sandbox, `SqlLocalDB info` identificou `MSSQLLocalDB` 17.0.4025.3, inicialmente parado. A instância foi iniciada para verificação e `sqlcmd` retornou `SELECT 1` com sucesso; o uso futuro requer execução com acesso ao perfil do usuário e `ConnectionStrings__FinanceManagerTests` configurada para bancos isolados.
- Node 24.22.2 está disponível em `.local-tools/node-v24.22.2-win-x64/node.exe`; usar esse diretório no PATH dos comandos de validação, sem alterar a instalação do usuário.
- Fora do sandbox, `dotnet dev-certs https --check --trust` confirmou certificado local válido e confiável.
- Nenhuma aplicação foi iniciada; a instância LocalDB iniciada para a verificação deve ser parada ao término desta execução e verificada.
- T001: versões fixadas, baseline Git, checklist, clarificação B e contratos revisados conferidos. Os requisitos de dados sintéticos e separação de bancos estão em `quickstart.md`.

## Implementação parcial — comparação de cadastro

- Criados `src/backend/FinanceManager.Domain/Authentication/OwnerProfileChanges.cs` e `tests/FinanceManager.UnitTests/Authentication/OwnerProfileChangesTests.cs` para mesclagem de campos e detecção de originais divergentes, incluindo desired == vigente, campos independentes e normalização.
- Teste direcionado: `dotnet test --project tests/FinanceManager.UnitTests/FinanceManager.UnitTests.csproj --no-restore --filter-class '*OwnerProfileChangesTests*'` fora do sandbox: 19 aprovados, 0 falhas, 0 ignorados.
- Tentativas anteriores não executaram testes: o build no sandbox não apresentou diagnóstico; a opção `--disable-build-servers` não é aceita pelo runner Microsoft.Testing.Platform do projeto. A execução válida removeu essa opção.
- T003/T004 permanecem abertas: coberturas e integração completas ainda precisam ser confirmadas. T002 e demais tarefas não foram concluídas; nenhuma tela, endpoint ou persistência foi entregue.

## Continuação — fundação da aplicação

- Adicionados `OwnerProfileResults.cs` e `IOwnerProfileStore.cs` com resultados/porta de atualização, e `OwnerProfileService.cs` com validação de senha opcional e data civil em America/Sao_Paulo. O tipo que contém senhas não expõe esses valores em `ToString()`.
- Adicionada fixture inicial `OwnerProfileScenario.cs`, com banco isolado, dois titulares sintéticos e fábrica HTTP com relógio controlável. Ainda faltam sessões, histórico financeiro e validação SQL exigidos por T002.
- Testes de domínio e aplicação `--filter-class '*OwnerProfile*'`: 21 aprovados, 0 falhas, 0 ignorados. Build do projeto de integração com `--disable-build-servers`: aprovado, 0 avisos e 0 erros.
- T002–T005 e demais tarefas permanecem pendentes: a fundação, a cobertura exigida, persistência, API e interface ainda não estão completas.

## Continuação — auditoria e contrato de entrada

- Criado `OwnerProfileMigrationTests.cs`: observado erro esperado antes da migração na constraint existente; após a migração, upgrade/downgrade protegido e preservação de dois titulares foram validados em LocalDB isolado: 1 teste aprovado.
- Gerada `20261122220534_AllowProfileUpdateAudit` e snapshot; downgrade recusa reversão se houver auditoria `ProfileUpdated`. `dotnet ef migrations has-pending-model-changes` informou ausência de mudanças pendentes.
- Adicionado parser localizado `OwnerProfileRequests.cs` para objetos estritos `changes`/`originalValues` e troca opcional de senha; testes de contrato HTTP ainda não foram executados.
- `dotnet build FinanceManager.slnx --no-restore --disable-build-servers`: aprovado, 0 avisos, 0 erros.
- T002, T006–T009 permanecem abertas: fixture/cobertura pedidas e validação de cenários completos ainda não foram concluídas. Nenhuma tela ou endpoint foi publicado.

## Continuação — endpoint e persistência inicial

- Criados `SqlOwnerProfileStore.cs` e `OwnerProfileController.cs`, com consulta autenticada, revalidação de sessão, comparação de originais sob bloqueio, persistência seletiva e gravação transacional com auditoria. Serviço e porta registrados na injeção de dependências.
- `OwnerProfileContractTests`: falha esperada 404 antes do endpoint; após a implementação, 3 cenários aprovados em LocalDB isolado: consulta/edição e rejeição de original obsoleto, acesso sem sessão e alteração combinada de cadastro/senha com login novo e antigo.
- Cobertura de autorização cruzada, CSRF, concorrência efetiva, bloqueio de credenciais, rollback e falhas de persistência ainda não foi concluída. Nenhuma tarefa foi marcada como concluída com base apenas nesses testes; interface ainda não existe.

## Continuação — tela de atualização cadastral

- Adicionados `owner-profile.service.ts` e `owner-profile.component.ts`, com formulário pessoal, troca opcional de senha, envio de originais por campo, tratamento inicial de conflito e revisão explícita; rota `/profile`, navegação autenticada e tratamento de expiração integrados.
- Instalação `npm ci` executada com Node 24.22.2 e cache local. Build Angular aprovado; testes direcionados `owner-profile*.spec.ts`: 5 aprovados, 0 falhas.
- Permanecem pendentes testes completos de acessibilidade/navegador, paridade de normalização Unicode entre frontend e backend, cenários de falha/concorrência e validação funcional de todas as tarefas. Não marcar tarefas completas apenas com esses resultados.

## Continuação — segurança e navegador interno

- Corrigida a fixture sintética para criar titulares com `SecurityStamp`, `ConcurrencyStamp` e bloqueio habilitado, conforme Identity. A falha inicial de senha atual retornando 500 foi reproduzida e resolvida na fixture; não se adicionou log de exceções ou credenciais.
- `OwnerProfileContractTests`: 10 testes aprovados, 0 falhas, incluindo CSRF ausente, seleção de terceiro rejeitada, senha incorreta com contagem persistida, e-mail duplicado, bloqueio após cinco erros, campos diferentes preservados, conflito com senha combinada e JSON inválido/duplicado. O teste de campos diferentes usa envios sequenciais com snapshots anteriores; corrida simultânea real ainda está pendente.
- No navegador interno do Codex e banco sintético isolado, confirmado login sem conta financeira, rota de perfil, campos carregados, gravação de nome com confirmação e exibição de três campos de senha vazios. A navegação inicialmente carregou uma versão em cache sem o novo link; após abrir `/profile`, o link e a tela atualizados apareceram. Não foi realizada troca de senha pela interface.
- Servidor de validação PID 17324 e filhos 42420/30868 encerrados; LocalDB encerrado e verificado parado. O banco isolado `FinanceManagerProfileValidation006` permanece para futura validação, sem dados reais.
- Permanecem pendentes regressão completa, falha/rollback injetada, corrida simultânea, limites de senha no endpoint, paridade Unicode, acessibilidade/foco e avaliação humana SC-006. Nenhuma tarefa adicional foi marcada como concluída.

## Revisão de conflito e atomicidade — 2026-11-22

- Revisão da interface passou a exigir uma escolha por campo conflitante (usar valor atual ou reaplicar o rascunho); sem escolhas, não atualiza a referência. Teste Angular agora cobre o bloqueio até a escolha explícita.
- Testes de domínio cobrem originais canônicos equivalentes com espaços, apelido, telefone mascarado e e-mail Unicode em caixa diferente.
- Teste HTTP simultâneo com duas sessões e o mesmo snapshot: uma edição do mesmo campo retorna 200, a outra 409 `PROFILE_UPDATE_CONFLICT`; apenas uma auditoria é gravada e o primeiro valor permanece.
- Teste de atomicidade cria trigger sintético que falha na auditoria durante alteração combinada. Resposta 500 contém a mensagem contratada; perfil, hash, SecurityStamp e auditorias permanecem sem alterações parciais.
- Revalidação: 23 testes unitários de OwnerProfile aprovados; 12 testes de contrato OwnerProfile aprovados em LocalDB isolado; 34 testes Angular de autenticação aprovados. LocalDB foi iniciado apenas para os testes, depois parado e confirmado.
- `dotnet build FinanceManager.slnx --no-restore` fora do sandbox: aprovado, 0 avisos e 0 erros. `npm run build`: aprovado.
- Regressão completa: `dotnet test` UnitTests 140/140 e IntegrationTests 109/109 aprovados, 0 ignorados; `npm test -- --include='src/app/auth/*.spec.ts'`: 34/34 aprovados.
- `dotnet ef migrations has-pending-model-changes` com `ConnectionStrings__FinanceManager` sintética: nenhuma diferença pendente após `20261122220534_AllowProfileUpdateAudit`.
- Avaliação humana SC-006 com cinco participantes permanece pendente; por isso, a feature não pode ser declarada completa.
