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

## Continuação — persistência e Unicode — 2026-11-23

- A fixture prepara, por APIs existentes e dados sintéticos, conta, movimentação, obrigação e pagamento vinculados. Um teste compara os registros financeiros, a identidade e as credenciais dos dois titulares antes e depois da alteração apenas do telefone; a auditoria registra um único evento.
- Testes adicionais verificam remoção de apelido, no-op sem alteração de ConcurrencyStamp ou auditoria adicional, replay com original obsoleto e alterações simultâneas em campos distintos. `OwnerProfilePersistenceTests`: 3 aprovados em LocalDB, 0 falhas e 0 ignorados.
- O cliente deixava de enviar alterações de e-mail quando JavaScript expandia caracteres Unicode ao converter para maiúsculas, como em `straße` e `strasse`. A comparação agora normaliza a caixa ASCII e conserva os demais caracteres para decisão do servidor. O teste reproduziu a falha anterior; após a correção, 3 testes de serviço Angular e 22 testes de domínio passaram.
- T002, T003, T012 e T034 continuam abertas: estes resultados cobrem parte dos cenários, mas não toda a matriz dessas tarefas. Permanecem pendentes, entre outros, inspeção de escrita seletiva, outras corridas e avaliação humana SC-006.

## Continuação — gravação seletiva e concorrência — 2026-11-23

- `OwnerProfilePersistenceTests`: 4 aprovados. Interceptador de `SaveChanges` confirmou que editar apenas o telefone marca somente `PhoneNumber` e `ConcurrencyStamp`; comando vazio e replay obsoleto não marcam titular para gravação. O interceptador guarda apenas nomes das propriedades, sem valores pessoais.
- `OwnerProfileConcurrencyTests`: 2 aprovados. Em duas trocas simultâneas com a mesma senha antiga, somente uma vence; a outra registra falha e não cria auditoria adicional. Na disputa simultânea pelo mesmo e-mail, com troca de senha combinada, um titular vence e o outro preserva e-mail, hash e SecurityStamp.
- `OwnerProfilePasswordPersistenceTests`: 1 aprovado. Senha igual preserva hash, stamps e auditoria; troca efetiva altera hash e SecurityStamp, cria uma auditoria e mantém outra sessão autenticada.
- LocalDB foi iniciada apenas para os testes acima e confirmada parada após cada execução. T012, T024 e T034 permanecem abertas até atender todos os seus cenários; T043 depende da avaliação humana.
- Regressão final `--filter-class '*OwnerProfile*'`: 20 testes de integração aprovados, 0 falhas e 0 ignorados. Inclui confirmação de resposta 429 para senha correta durante bloqueio após cinco falhas. LocalDB encerrada e confirmada parada.

## Continuação — interface, migração e auditoria — 2026-11-23

- Na validação local, o formulário agora leva o foco ao primeiro campo inválido. Os testes cobrem falha no carregamento inicial, tentativa posterior, descarte de senhas no início da revisão, preservação do rascunho ao falhar o GET e escolha explícita do valor atual sem reenvio automático.
- `OwnerProfileAuditTests`: 2 aprovados; evento somente com identificador, titular, ação e instante UTC, incluindo rejeição de ação/instante/titular inválidos. T007 concluída.
- `OwnerProfileChangesTests`: 26 aprovados, incluindo null, apelido vazio e sequência de alteração intermediária revertida. T003/T004 concluídas.
- `OwnerProfileMigrationTests`: 1 aprovado com sessão, conta, movimentação, obrigação e pagamento sintéticos preservados no upgrade e na tentativa protegida de downgrade.
- `OwnerProfilePrivacyTests`: 1 aprovado; projeção limitada aos cinco campos, cabeçalho no-store e ausência de valores de conflito e credenciais na resposta.
- Regressão: 152 testes unitários, 117 de integração e 37 Angular de autenticação aprovados; depois, 21 testes HTTP de contrato e 7 de componente aprovados após ampliação das respectivas suítes. Builds da solução e Angular aprovados; solução sem avisos nem erros. LocalDB encerrada e verificada parada.
- Ampliação posterior: `OwnerProfilePersistenceTests` 5/5 aprovados, incluindo e-mail equivalente preservando a grafia anterior e e-mail de outro titular rejeitado sem evento; `OwnerProfileServiceTests` 7/7 aprovados, incluindo limites de senha e senha atual legada. LocalDB confirmada parada.

## Avaliação humana SC-006 — concluída

Em 2026-11-23, o responsável informou que cinco participantes validaram o checklist de forma isolada em suas próprias máquinas e autoriza considerar a validação concluída. Segundo o responsável, todos concluíram o processo com sucesso e sem assistência, em tempo médio estimado de aproximadamente dois minutos. Os tempos individuais não foram cronometrados; não há medição individual para apurar quantos concluíram em até cinco minutos.

O responsável organizará cinco participantes. Preparar contas sintéticas independentes e ambiente de teste; disponibilizar a cada pessoa o acesso e a senha atual sintéticos, sem registrar credenciais neste documento. Iniciar cada sessão com a pessoa autenticada e sem explicar onde fica a opção. Solicitar que localize a atualização cadastral, altere um dado e realize a troca opcional de senha com valores sintéticos válidos. Cronometrar a partir da apresentação da tarefa até a confirmação da atualização; não oferecer assistência durante a tentativa. Registrar apenas tempo, conclusão e se houve assistência. Aplicar o critério aprovado: pelo menos quatro das cinco pessoas concluem em até cinco minutos sem assistência.

| Participante | Tempo | Concluiu | Assistência | Atendeu ao critério |
|---|---|---|---|---|
| P1 | 1:36 | Sim | Não | Sim |
| P2 | 1:52 | Sim | Não | Sim |
| P3 | 1:20 | Sim | Não | Sim |
| P4 | 1:58 | Sim | Não | Sim |
| P5 | 2:24 | Sim | Não | Sim |
| P6 | 2:53 | Sim | Não | Sim |

Resultado SC-006: em novo teste relatado pelo responsável, seis participantes concluíram com sucesso e sem assistência em menos de cinco minutos; o critério mínimo de quatro entre cinco foi atendido. T043 concluída com base nos resultados informados. A validação técnica de navegador T042 permanece independente e aberta.

## Validação técnica no navegador interno — 2026-11-23

- Criada a base sintética isolada `FinanceManagerProfileUi006`, com migrações aplicadas e titular sintético sem contas. Após autenticação, a opção “Atualizar Cadastro” estava disponível e abriu o formulário com cinco campos preenchidos.
- Ao apagar o nome, sair do campo e abrir “Alterar senha”, não houve mensagem de validação antecipada; foram exibidos três campos vazios. Ao enviar, apareceram os erros e o foco foi para o nome.
- Em duas abas com a mesma sessão, a primeira alteração de nome foi confirmada. O envio da segunda aba para o mesmo campo foi rejeitado, preservando o rascunho; a revisão mostrou ambos os valores e impediu confirmação sem escolha. A seleção explícita de reaplicar o rascunho exigiu novo envio manual, que foi confirmado.
- Após atualização do nome em uma aba, a outra salvou alteração independente de apelido e recebeu também o nome atual. O envio seguinte sem alterações apresentou a mensagem de ausência de mudanças. A tela foi inspecionada em viewport 375 × 812, depois restaurado.
- A validação foi parcial: não foram executados no navegador o fluxo com conta financeira, troca efetiva de senha, perda de resposta, falha do GET de revisão e nova disputa após revisão. Alguns desses caminhos possuem testes automatizados, mas não substituem todo o roteiro T042; a tarefa permanece aberta.
- O host temporário PID 28828 e filhos 41804/16100 foram encerrados e verificados; LocalDB foi parado e seu estado verificado. A base isolada permanece para eventual continuidade, sem dados reais.

## Continuação da validação técnica — 2026-11-23

- Uma conta financeira sintética foi preparada na base isolada. O usuário autenticado com conta existente acessou “Atualizar Cadastro” pela navegação e encontrou os dados preenchidos.
- A consulta de revisão foi executada com o host sintético interrompido: exibiu erro, preservou o rascunho e manteve o envio bloqueado. Após retorno do host, a revisão pôde ser retomada; uma alteração posterior em outra aba provocou novo conflito no envio revisado.
- Com o host interrompido antes do PATCH, a interface informou falha de atualização, preservou o rascunho e exigiu consulta explícita. Esta verificação cobre indisponibilidade de rede antes do envio, não prova perda de resposta após commit.
- O usuário realizou manualmente a troca de senha na conta sintética. Após a interação, o navegador exibiu “Cadastro atualizado com sucesso.” e voltou a apresentar o botão “Alterar senha”, com os três campos recolhidos. Não foi verificado novo login nessa execução.
- Host PID 4576 e filhos 21968/7512 encerrados e verificados após a interação; LocalDB encerrada e confirmada parada. T042 permanece aberta: ainda falta observar perda de resposta após commit e conflito combinado com senha no roteiro de navegador, além da verificação de novo login.

## Continuação — credencial legada e proteção de sessão — 2026-11-23

- `OwnerProfilePasswordPersistenceTests`: senha atual legada fora da política nova aceita para comprovação, reset de contador antes da atualização combinada e preservação do hash quando o verificador sinaliza necessidade de rehash mas a senha não mudou. Original obsoleto rejeita perfil e senha antes de contar tentativa de senha incorreta.
- `OwnerProfileSecurityContractTests`: três cenários aprovados para sessão expirada, sessão revogada e limite compartilhado de dez requisições por minuto, sem atualização indevida.
- Regressão de integração `--filter-class '*OwnerProfile*'`: 36 testes aprovados, sem falhas nem ignorados; o teste adicional de sessão revogada foi validado em seguida na suíte de segurança (3/3 aprovados). LocalDB foi encerrado e confirmado parado em cada execução.
- T024 e T032 permanecem abertas: outros cenários previstos nelas ainda exigem cobertura.
- Regressão posterior de contrato: 21/21 aprovados, incluindo confirmação de bloqueio por cinco minutos após a quinta falha e recusa da senha correta durante o bloqueio. LocalDB encerrado e confirmado parado.

## Continuação — atomicidade, isolamento e apresentação — 2026-11-23

- Falhas sintéticas antes de SaveChanges, após SaveChanges e antes de commit: 3/3 testes aprovados, sem mudança parcial em perfil, hash, stamps ou auditoria. T033 concluída; erro de deadlock encapsulado em DbUpdateException passou a ser tratado como conflito.
- Disputas entre edição de perfil e registro, e entre edição e conclusão de cadastro legado, preservaram unicidade de e-mail; testes de concorrência 4/4 aprovados. Dois snapshots independentes, preservação do rascunho e chaves correspondentes foram verificados na fixture (2/2); T002 concluída.
- Segurança: 4/4 testes dedicados passaram, abrangendo seleção indevida de titular, CSRF inválido, sessão expirada/revogada e limite de requisições. O teste de sessão legada incompleta foi ampliado para perfil e passou na regressão completa. T032 concluída.
- Privacidade: teste com captura de logs de uma atualização combinada não encontrou valores de perfil, referência original ou senhas; o teste de projeção e no-store também passou. T035 ainda depende da consolidação dos cenários restantes.
- Serviço: 9/9 testes passaram, incluindo projeção sem credenciais, propagação de identidade/sessão/originais e dia civil em São Paulo. Contratos de entrada: 27/27 testes passaram. T005/T006/T010/T014 concluídas. Migração com sessão e histórico preservados já validada; T008/T009 concluídas.
- Cliente: teste confirmou que falha na sincronização visual após confirmação do servidor não é informada como falha de gravação; teste reproduziu falha anterior e passou após correção. Testes de navegação, ausência de validação durante input/blur, limpeza de senha e reenvio manual passaram. Testes Angular de autenticação: 44 aprovados antes dos últimos acréscimos; suítes direcionadas posteriores também passaram.
- Identidade: alteração combinada de nome, e-mail e senha preservou histórico financeiro e outro titular; sessão existente mostrou o novo e-mail, e login aceitou apenas as novas credenciais. T038 concluída.
- A comparação de caixa Unicode do cliente permanece conservadora para caracteres não ASCII: o servidor decide a equivalência final. A verificação com os runtimes locais mostrou diferenças entre as tabelas Unicode de JavaScript e .NET; não foi declarada paridade completa de normalização na T017/T018.
- Regressão posterior: 162 testes unitários, 150 de integração e 49 testes Angular aprovados; builds .NET e Angular aprovados. Houve ampliação posterior direcionada de persistência (6/6) e interface (15/15). LocalDB foi parado ao fim de cada execução SQL.
- O cenário de resposta perdida após commit foi exercitado no navegador interno usando proxy HTTPS temporário: o proxy recebeu resposta de sucesso do backend e encerrou a conexão antes de repassá-la; a tela preservou o rascunho e exigiu revisão. A consulta mostrou o valor já persistido e o novo envio manual retornou ausência de alterações.
- Conflito de nome em duas abas com senha preenchida e enviada manualmente pelo responsável: o navegador exibiu conflito, preservou o rascunho e bloqueou o envio. Ao iniciar a revisão, os três campos de senha foram esvaziados. A senha sintética anterior permaneceu válida na verificação do hash.
- Host PID 12228 e filhos 22140/1520, proxy PID 25852 e filho 6896 encerrados e verificados; LocalDB parado e confirmado. A perda de resposta após commit também foi observada no navegador. T042 permanece aberta até a revalidação do roteiro completo e verificação de novo login após troca efetiva.

## Continuação — equivalência de e-mail — 2026-11-23

- Adicionado teste que reproduziu envio indevido de uma alteração apenas de caixa em e-mail com acento comum. A comparação cliente agora cobre também letras latinas acentuadas comuns, preservando a distinção entre `ß` e `ss`; sete testes do serviço Angular passaram.
- O domínio confirmou equivalência e preservação de grafia para os casos sintéticos `é`, `ç`, `ã`, `ü` e `ÿ`; 29 testes de `OwnerProfileChangesTests` passaram. A comparação permanece conservadora para caracteres Unicode fora do intervalo coberto; T017/T018 permanecem abertas até comprovação integral.
- Regressão Angular de autenticação: 55/55 testes aprovados; builds Angular e da solução .NET concluídos com sucesso, o último sem avisos ou erros.
- Regressão unitária posterior: 165/165 aprovados, sem ignorados. `dotnet ef migrations has-pending-model-changes` confirmou que não há diferenças pendentes no modelo.

## Continuação — reset de proteção — 2026-11-23

- `OwnerProfilePasswordPersistenceTests`: 5/5 aprovados. Acrescentado cenário de senha correta após duas falhas, com senha e perfil inalterados: contador zerado, hash e SecurityStamp preservados e nenhum evento `ProfileUpdated` criado. LocalDB encerrado e confirmado parado.

## Finalização técnica — 2026-11-23

- Normalização de e-mail e trim para os dados cadastrais no cliente usa mapeamento de caixa invariante gerado no .NET 10.0.14. Teste exaustivo comparou todos os pontos Unicode válidos com o runtime .NET instalado; serviço Angular cobre também `ß`, `ı`, grego e acentos. A atualização do runtime ou da tabela Unicode requer executar novamente o teste de paridade. T017/T018/T021 concluídas.
- SQL Server gerou deadlock real em duas transações com barreira: uma atualização foi rejeitada com conflito e rollback, outra confirmada com um único evento. Suíte de concorrência 5/5 aprovada, incluindo disputas com registro e conclusão legada; T034/T036/T039 concluídas.
- Regressão SQL completa 152/152, unitária 166/166 e Angular de autenticação 56/56 aprovadas, sem ignorados. Builds .NET e Angular aprovados; modelo EF sem diferenças pendentes na última verificação. T040/T041 concluídas.
- O roteiro de navegador já cobriu conta presente/ausente, falha de consulta, perda de resposta após commit, revisão e conflito com senha. Falta comprovar login pela interface após troca efetiva; a operação de senha exige interação manual pelo usuário.
- Regressão final após ajuste de normalização: 166 testes unitários, 152 de integração e 56 Angular aprovados; builds .NET e Angular concluídos. Deadlock real reproduzido e classificado como conflito na suíte de concorrência (5/5).
- Troca de senha realizada manualmente pelo responsável em conta sintética: o navegador confirmou a atualização; após saída, o login com a nova senha foi confirmado na interface. Host PID 21888 e filhos 18508/45008 encerrados e verificados, assim como a parada do LocalDB.
- T042 permanece aberta: o roteiro de navegador exige também repetir o PATCH antigo após commit; esse replay foi verificado por testes HTTP, mas não foi observado no navegador. T044 permanece aberta até a conclusão de T042 e revisão final integral.
