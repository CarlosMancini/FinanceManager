# Evidências de validação · 004

## Fase 1 · T001 — baseline validado

- Checklist de requisitos: 16/16 itens marcados.
- SDK .NET 10.0.403 e dotnet-ef 10.0.14 correspondem aos manifestos. `dotnet tool restore` e `dotnet restore .\FinanceManager.slnx --locked-mode` passaram com acesso à configuração NuGet do usuário.
- Node global 24.13.0 diverge do 24.22.2 fixado. O Node 24.22.2 foi instalado em `.local-tools/node/`, diretório ignorado pelo Git, sem alterar manifestos ou lockfiles.
- A restauração Angular foi concluída pelo `npm-cli.js` executado diretamente com o Node 24.22.2: `npm ci --no-audit --no-fund` instalou 280 pacotes. `npm ls --depth=0` passou e confirmou as versões diretas do manifesto. Tentativas anteriores interrompidas foram substituídas por essa restauração concluída.
- `.gitignore` existente cobre os artefatos da stack; nenhum hook de extensão configurado foi encontrado.

T001 concluída.

## Fase 1 · T002 — parcialmente validada

- Criadas fixtures para banco vazio e para titular legado com hash, sessão, conta, movimentação, obrigação e baixa sintéticos na migração 003.
- `dotnet build` do projeto de integração passou sem avisos ou erros. Dois testes das fixtures passaram em bancos LocalDB isolados (2/2), incluindo verificação de senha sintética e vínculos financeiros.
- Após a geração da migração 004, foi adicionada a fixture de titular com cadastro concluído. A fixture legada insere a identidade usando as colunas da migração 003 antes de aplicar a migração 004, sem depender do modelo EF atualizado.
- Os três testes dos cenários vazio, concluído e legado passaram no LocalDB. T002 foi marcada como concluída.

## Fase 2 · validação

- T003–T014 concluídas. As regras de perfil, senha e autorização incluem limites, caracteres Unicode, expiração absoluta, revogação e virada do dia em São Paulo. A suíte unitária completa passou: 82 testes.
- A migração foi validada em LocalDB para banco vazio e atualização do esquema 003. Testes verificaram preservação de identidade e vínculos financeiros, rejeição de múltiplos titulares legados, slot único, perfil obrigatório, prazo e hash da autorização, auditoria e remoção do índice antigo de e-mail antes da alteração da coluna. `dotnet ef migrations has-pending-model-changes` confirmou que o snapshot corresponde ao modelo.
- Validadores Identity foram testados com e-mail longo, senha Unicode e senha legada; o contrato HTTP de validação foi verificado em quatro testes, inclusive CSRF em mutação anônima. Sessões incompletas foram testadas com cookie legado real nas três rotas financeiras, com controle de sessão válida e expiração aos 30 minutos.
- A suíte completa de integração passou em SQL Server LocalDB: 40 testes. A suíte Angular existente passou: 25 testes em quatro arquivos. A suíte de integração de contas foi adaptada para verificar leitura e escrita com identificador de titular diferente sem inserir um segundo titular, proibido pela nova restrição.
- A fase 2 prepara a fundação. Os fluxos de cadastro, login definitivo e transição pertencem às fases seguintes e não foram implementados nesta fase. A atualização não deve ser disponibilizada a instalações legadas antes da conclusão da transição.

## Fase 3 · US1 — cadastro inicial

- T015–T025 concluídas. O cadastro inicial valida os dados no serviço, persiste titular, hash e auditoria na mesma transação e não cria sessão. O endpoint orienta login quando já existe cadastro e transição quando há titular legado.
- Testes no SQL Server LocalDB cobriram campos obrigatórios e entrada inválida, CSRF, dois cadastros concorrentes com e-mails iguais e diferentes, rollback de falha na auditoria, repetição após resposta perdida e preservação após reiniciar a aplicação. O titular legado e sua baixa permaneceram intactos.
- A interface foi validada com dados sintéticos em base local isolada: campos vazios exibiram mensagens, critérios de senha foram apresentados e o cadastro válido retornou ao login com confirmação e senha vazia. O contrato HTTP também verificou ausência de sessão após cadastro.
- Suítes finais: 94 testes unitários, 54 de integração e 32 testes Angular passaram. O build Angular passou. O comando `provision-owner` retorna orientação para criar a conta pela interface, sem provisionar titular.
- Esta validação cobre somente US1. Login definitivo por e-mail pertence à US2, e a transição de instalações legadas pertence à US3; não disponibilizar a atualização a instalações legadas antes de concluir essas histórias.

## Fase 4 · US2 — login por e-mail

- T026–T035 concluídas. O login utiliza exclusivamente e-mail de titular concluído, comparado com trim e caixa normalizada, e verifica a senha exatamente como fornecida. A sessão apresenta apenas o e-mail; o contrato antigo com username é rejeitado. A leitura da sessão é delegada ao serviço de aplicação (FR-005/007/009/012/014).
- SQL Server LocalDB e HTTP: 61 testes passaram, incluindo e-mail com caixa e espaços diferentes, rejeição de nome legado, senha incorreta, DTO antigo, titular legado incompleto, cookie seguro, CSRF, saída e replay, inatividade no limite de 30 minutos, cinco falhas com bloqueio de cinco minutos e 11ª tentativa por IP limitada sem depender de bloqueio da conta (FR-009/012–014, SC-003/006). O endpoint de prova legado e sua utilização conjunta do limitador pertencem à US3.
- Testes unitários .NET: 95 passaram, incluindo criação e reutilização de sessão, recusa após revogação e expiração. Testes Angular: 41 passaram, incluindo restauração e bloqueio de sessão ausente, erros de login, expiração em obrigações e navegação pelas rotas MOV-05 com zero, contas ativas e somente arquivadas (FR-001/011–014).
- O build Angular passou. Em navegador com banco local sintético isolado, login por e-mail direcionou à criação de conta quando não havia contas; recarregar preservou a sessão e sair retornou ao login com senha vazia. Esses testes não dependem do cadastro durante o fluxo de login (FR-001/009/011/012).
- A medição humana de SC-001 continua pendente da fase própria. A transição de instalações legadas pertence à US3 e deve ser concluída antes de disponibilizar a atualização a essas instalações.

## Fase 5 · US3 — transição legada em validação

- T036–T048 implementadas e validadas. O titular legado comprova nome e senha anteriores e recebe autorização restrita, com token aleatório apenas em cookie seguro e hash em SQL. Login e prova compartilham o limitador; a autorização não concede sessão financeira (FR-012/013/015/017).
- Emissão e conclusão coordenam a mesma linha do titular com bloqueio SQL. A conclusão preserva Owner.Id, atualiza perfil e credencial, revoga sessões e autorizações anteriores e registra auditoria na mesma transação (FR-014/016).
- Testes SQL verificaram limite de 599, 600 e 601 segundos; espera de dois minutos deixou oito minutos e espera de dez minutos não emitiu autorização. Duas conclusões sincronizadas tiveram um vencedor; prova anterior não foi emitida após conclusão; login concorrente não acessou titular incompleto. Falha de auditoria reverteu perfil, hash, sessões e autorizações; repetição não sobrescreveu o vencedor (SC-004/006/007).
- Na migração sintética desde 003 foram comparados os registros completos de contas, movimentações, obrigações e baixas antes e depois, incluindo IDs, vínculos, valores e origens. Cookies anteriores e autorizações de transição foram rejeitados; login por e-mail funcionou após reiniciar a aplicação (SC-003/005/006).
- Passaram 98 testes unitários, 76 de integração SQL e 47 Angular; build Angular e `dotnet ef migrations has-pending-model-changes` passaram. Os testes Angular cobrem ambos os caminhos, renovação da prova após expiração preservando dados não secretos, resposta perdida e descarte de senhas.
- No navegador integrado, uma nova base sintética isolada preparada pela fixture legada permitiu comprovar o acesso anterior pelo caminho Criar conta → Completar cadastro existente. Após o envio da nova senha pelo usuário, a aplicação retornou ao login com confirmação e senha vazia; o login por e-mail abriu o histórico com a despesa sintética de R$ 42,00. Após sair, o nome de acesso anterior foi recusado. Uma comparação integral dos registros de Owner.Id, contas, movimentações, obrigações e baixas antes e depois apresentou conteúdo idêntico.
- Na retomada da T049, passaram os 3 testes unitários de transição, 37 testes SQL de cadastro e transição e 47 testes Angular; o build Angular também passou. Uma tentativa Angular no ambiente restrito falhou ao iniciar os processos do Vitest e passou após execução com acesso ao ambiente do projeto. Os testes SQL anteriores e desta retomada fornecem as evidências de limite temporal, concorrência, rollback e replay descritas acima. T049 concluída; T050–T054 permanecem pendentes.

## Fase 6 · verificações em andamento

- Passaram 98 testes unitários .NET, 78 testes de integração SQL e 49 testes Angular. O build Angular e `dotnet ef migrations has-pending-model-changes` passaram; a migração completa também foi aplicada a uma base sintética vazia. A primeira tentativa de verificação EF falhou por ausência da conexão de design e passou após configurá-la.
- Evidências parciais de segurança e interface estão em `validation-security.md` e `validation-ui.md`. T050 e T051 ainda exigem os cenários ali apontados; T052 permanece aberta até a conclusão de suas dependências e revisão da matriz. T053 está pendente pela indisponibilidade de participantes, registrada em `validation-usability.md`; T054 depende das evidências finais.

## Retomada das verificações · 2026-11-13

- Em execução sequencial, passaram 98 testes unitários .NET, 78 testes de integração SQL Server LocalDB e 49 testes Angular; os builds da API e Angular passaram. `dotnet ef migrations has-pending-model-changes --no-build` confirmou que não há alterações pendentes no modelo.
- No sandbox, a proteção de dados do ASP.NET falhou ao acessar as chaves locais e causou HTTP 500 na obtenção de CSRF. A suíte de integração completa passou ao executá-la no ambiente normal. O Node 24.22.2 fixado está em `.local-tools/node/node_modules/node/bin/node.exe`.
- Essas verificações não encerram T050–T052: continuam necessárias a inspeção sistemática de privacidade, a validação interativa dos fluxos de interface e a consolidação da matriz de verificação. T053 e T054 permanecem pendentes.

## Nova retomada · 2026-11-13

- Após ampliar a verificação de privacidade da transição, passaram 98 testes unitários, 78 de integração SQL e 49 testes Angular, além do build Angular. A compilação sequencial do projeto de integração passou sem avisos; os testes .NET e Angular foram executados fora do sandbox por limitações de acesso a pipes/processos. A primeira tentativa das suítes .NET recebeu opções de compilação incompatíveis com o executor e não executou testes; a execução corrigida passou.
- T050–T054 continuam abertas: a ampliação dos testes não substitui a inspeção sistemática de privacidade nem a validação interativa; a avaliação com cinco participantes permanece pendente por decisão do responsável.

## Consolidação parcial da matriz · 2026-11-13

| Requisitos | Evidências automatizadas e interativas registradas |
|---|---|
| FR-001–002 | `login.component.spec.ts`, `registration.component.spec.ts`, `auth.service.spec.ts` e navegação de cadastro/retorno no navegador. |
| FR-003–007 | `RegistrationProfileTests`, `RegistrationPasswordPolicyTests`, `RegistrationIdentityValidationTests`, `RegistrationContractTests` e `registration-validation.spec.ts`. |
| FR-008/014 | `RegistrationPersistenceTests`, `RegistrationContractTests` e `OwnerRegistrationServiceTests`: concorrência, falhas, reenvio e validação. |
| FR-009–013 | `OwnerAccessServiceTests`, `AuthContractTests`, `OwnerSessionPersistenceTests`, `IncompleteOwnerAccessTests`, testes Angular de autenticação e `RegistrationPrivacyTests`. A inspeção de privacidade ainda está em andamento. |
| FR-015–017 | `OwnerRegistrationTransitionServiceTests`, `RegistrationTransitionContractTests`, `RegistrationTransitionPersistenceTests`, `LegacyRegistrationMigrationTests` e testes Angular de transição. A validação interativa dos estados restantes permanece pendente. |
| SC-001 | Avaliação humana T053 pendente; as verificações automatizadas não a substituem. |

- Após acrescentar teste de log em mutação financeira, passaram os três testes de privacidade e os 79 testes de integração SQL; a compilação do projeto de integração passou sem avisos e `has-pending-model-changes` confirmou o modelo atualizado. As demais suítes e builds constam da retomada anterior.
- Revisão arquitetural parcial: os controllers recebem serviços de aplicação; a busca nos controllers não identificou acesso direto a `DbContext` ou repositórios. A revisão final T054 continua condicionada às evidências pendentes, inclusive SC-001.

## T050 · segurança concluída

- `RegistrationPrivacyTests` e os contratos de autenticação, cadastro e transição passaram em SQL Server LocalDB: 27 testes. A compilação do projeto de integração passou sem avisos. A cobertura inclui rejeição de CSRF nas 16 rotas de mutação e inspeção de logs e respostas nos fluxos sintéticos; revisão de HTTPS, cookies, auditoria e consulta de login está detalhada em `validation-security.md`.
- T050 concluída. T051–T054 mantêm seu estado anterior; SC-001 continua pendente.

## T051 · interface concluída

- Validados no navegador integrado os caminhos legados, erro e foco, autorização expirada, nova prova e formulário completo em tela estreita; a preservação dos dados na expiração é coberta por teste Angular. Corrigido o texto de progresso da comprovação. Suíte Angular: 50 testes; build Angular passou. Detalhes em `validation-ui.md`.
- T051 concluída. T052–T054 permanecem abertas; SC-001 depende da avaliação humana T053.

## T052 · suítes e matriz consolidadas

- Em 2026-11-13, com SQL Server LocalDB e Node 24.22.2, `dotnet build FinanceManager.slnx --no-restore -m:1 /nodeReuse:false /p:UseSharedCompilation=false` passou sem avisos; `dotnet test --project tests/FinanceManager.UnitTests/FinanceManager.UnitTests.csproj --no-build` passou com **98/98**; o comando equivalente para `FinanceManager.IntegrationTests` passou com **80/80**. `dotnet ef migrations has-pending-model-changes --project src/backend/FinanceManager.Infrastructure --startup-project src/backend/FinanceManager.Api --no-build` não encontrou mudanças pendentes.
- `npm --prefix src/frontend test` passou com **50/50** e `npm --prefix src/frontend run build` passou, executados com o Node fixado em `.local-tools/node-v24.22.2-win-x64`. As suítes incluem as regressões de autenticação/sessão, contas, movimentações, obrigações e baixas das features 001–003; seus contratos, persistência e migrações estão nos respectivos diretórios dos testes de integração.
- Matriz do plano: FR-001–007 e SC-002 estão cobertos pelos testes de perfil, senha, identidade, contratos e formulário; FR-008/014 e SC-004 pelos testes de cadastro concorrente, reenvio e rollback; FR-009–013 e SC-003/006 pelos testes de autenticação, sessão, navegação, privacidade e interface; FR-015–016 e SC-004/005 pelos testes de migração legada, comparação de registros, concorrência e rollback; FR-017 e SC-007 pelos testes de prazo absoluto, atraso, expiração e nova prova. Os cenários interativos e as limitações estão detalhados nas seções anteriores e em `validation-ui.md` e `validation-security.md`.
- `RegistrationMigrationTests` verifica migração de banco vazio e rejeição de múltiplos titulares legados; `LegacyRegistrationMigrationTests` aplica a migração sobre histórico sintético e compara registros e vínculos antes/depois. A aplicação integral da migração em base sintética vazia e as verificações de navegador estão registradas nas fases 5 e 6 acima. Nenhum teste foi removido ou enfraquecido nesta validação.
- T052 concluída. A avaliação humana SC-001/T053 continua pendente e não foi substituída pelas suítes; T054 depende dessa evidência final.

## T054 · revisão técnica parcial

- **Arquitetura e constituição:** `FinanceManager.Domain` não declara referências de projeto e a busca em seu código não encontrou imports de ASP.NET Core, EF Core ou infraestrutura; `FinanceManager.Application` referencia o domínio. Os controllers recebem serviços de aplicação e a busca em seus arquivos não encontrou `DbContext` nem repositórios. As verificações de SQL Server, autenticação, CSRF, cookies e loopback estão nas evidências T050 e T052. A revisão é limitada à feature 004 e aos pontos de integração examinados.
- **Regras e casos de uso:** `RegistrationProfileTests` cobre normalização, obrigatoriedade, nascimento e limites de telefone; `RegistrationPasswordPolicyTests` cobre categorias, Unicode e limites; `RegistrationAuthorizationTests` cobre prazo e revogação; `OwnerSessionTests` cobre inatividade; `OwnerRegistrationServiceTests`, `OwnerRegistrationTransitionServiceTests` e `OwnerAccessServiceTests` cobrem os casos de uso e rejeições. Contratos HTTP, transações SQL, migrações e interface complementam esses testes conforme a matriz T052.
- **Convenções e diff:** os arquivos e identificadores da alteração revisada usam inglês; as mensagens de autenticação e erros examinadas estão em português, com códigos técnicos estáveis. A alteração funcional pendente restringe-se ao indicador de progresso da prova legada e possui teste Angular; o restante do diff contém testes e evidências da 004. `git diff --check` passou, com avisos de conversão de fim de linha. O diretório não rastreado `sites/` preexistente não foi incluído na revisão da feature.
- **Aceite pendente:** T001–T052 estão marcadas como concluídas; T053 e SC-001 permanecem pendentes por falta dos cinco participantes, conforme `validation-usability.md`. A tarefa T054 exige todas as evidências, inclusive SC-001: a revisão técnica acima não encerra T054 nem autoriza declarar a feature concluída. Após T053, revisar seus resultados e completar a verificação de aceite antes de marcar T054.
