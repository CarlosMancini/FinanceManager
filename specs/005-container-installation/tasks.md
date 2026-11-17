---
description: "Tarefas para implementar e validar a instalação local em contêineres"
---

# Tarefas da spec 005 — Instalação local em contêineres

**Referências** [Especificação](spec.md), [plano](plan.md), [pesquisa](research.md), [estado persistente](data-model.md), [contrato operacional](contracts/installation.md) e [matriz de validação](quickstart.md).

## Fase 1 — Preparação

- [X] T001 [P] Criar `.dockerignore` para excluir segredos, certificados, backups e artefatos locais do contexto de build (FR-004).
- [X] T002 [P] Criar `.env.example` com referências de imagem, identidade estável do projeto, configuração de credenciais por função, caminhos dos certificados e diretório de backup. Excluir a configuração real em `.gitignore` (FR-004, FR-007).

## Fase 2 — Infraestrutura compartilhada

- [X] T003 [P] Criar `Dockerfile` multiestágio para compilar Angular com npm ci, restaurar NuGet em modo travado, publicar ASP.NET Core para Linux amd64 e gerar o bundle EF Core da mesma revisão. Configurar a imagem da API para usuário sem privilégios (FR-001, FR-005).
- [X] T004 [P] Implementar em `src/backend/FinanceManager.Api/Program.cs` o modo explícito de escuta HTTPS em contêiner, mantendo a validação de localhost e a escuta nativa em loopback fora desse modo (FR-001, FR-002).
- [X] T005 [P] Configurar em `src/backend/FinanceManager.Api/Authentication/OwnerAuthentication.cs` o diretório persistente e a proteção das chaves em repouso com o certificado de Data Protection montado, preservando o comportamento nativo (FR-003, FR-004).
- [X] T006 Criar `compose.yaml` com aplicação, SQL Server 2025 Express e migrador. Publicar apenas `127.0.0.1:7043:7043`, montar certificados somente para leitura, configurar volumes do SQL e das chaves e aguardar consulta SQL autenticada e migração bem-sucedida antes de liberar a API (FR-001 a FR-005, FR-007). Depende de T003–T005.
- [X] T007 Implementar em `scripts/container/ContainerOperations.ps1` a validação de configuração e do servidor Docker em modo Linux com Engine 28 ou superior, rejeitando versões anteriores com mensagem em português, a execução exclusiva por instalação, a propagação de falhas sem segredos e a obtenção de imagens identificadas. Preservar a identidade do projeto e os nomes dos volumes (FR-003 a FR-008). Depende de T006.

## Fase 3 — US1 Instalação inicial (P1)

**Validação independente** Executar a instalação em máquina limpa e criar uma conta pela interface HTTPS. Confirmar recusa pela LAN e mensagem acionável para configuração inválida.

- [X] T008 [US1] Implementar em `scripts/container/Install.ps1` a validação de credenciais e PFX, a obtenção de imagens e a inicialização em ordem. Após o SQL ficar saudável e antes da migração, provisionar o banco, as identidades e as permissões com a credencial administrativa. Permitir reexecução sem recriar o banco ou apagar dados. Usar credencial com permissão de esquema para o migrador e credencial limitada à operação para a API. Encerrar com falha se algum pré-requisito impedir o cadastro (FR-001, FR-002, FR-004, FR-005, FR-007).
- [X] T009 [P] [US1] Disponibilizar manualmente em registro de teste a imagem inicial gerada pelo Dockerfile e registrar seu digest e a referência fixada do SQL Server em `specs/005-container-installation/quickstart.md` (FR-007). Depende de T006.
- [ ] T010 [US1] Executar os casos de instalação limpa e configuração inválida em `specs/005-container-installation/quickstart.md`. Confirmar cadastro, HTTPS confiável, porta SQL sem publicação, recusa pela LAN e falha de configuração sem exposição de segredos. Usar volume SQL vazio e baixar a imagem inicial do registro em ambiente sem cópia local. Repetir a instalação com o mesmo volume e verificar que a credencial da API opera, mas tem uma alteração de esquema rejeitada. Verificar a mensagem para Engine abaixo de 28 e a recusa na LAN no ambiente suportado (SC-001, SC-004). Depende de T008 e T009.

## Fase 4 — US2 Preservação dos dados (P1)

**Validação independente** Cadastrar dados sintéticos, recriar os contêineres com os mesmos volumes e confirmar acesso e registros.

- [X] T011 [P] [US2] Implementar `scripts/container/Stop.ps1` para parar os serviços sem remover volumes, respeitando a execução exclusiva por instalação (FR-003, FR-008).
- [X] T012 [US2] Executar o cenário de recriação em `specs/005-container-installation/quickstart.md`. Confirmar reaproveitamento de banco e chaves, acesso à conta e reexecução das migrações sem reaplicar as já registradas (FR-003, FR-005, SC-002). Depende de T008 e T011.

## Fase 5 — US3 Atualização e recuperação (P2)

**Validação independente** Atualizar uma instalação Docker com dados sintéticos, simular falha de migração e restaurar o backup em ambiente isolado.

- [X] T013 [US3] Implementar em `scripts/container/Update.ps1` o fluxo que baixa a nova imagem, para a API, cria backup SQL fora do volume antes da alteração de esquema e executa a migração da nova versão. Identificar o backup pela versão anterior e data e só iniciar a nova API após sucesso (FR-005 a FR-008). Depende de T008.
- [X] T014 [P] [US3] Implementar `scripts/container/Restore.ps1` com a entrada `-BackupPath`, validação do backup e da versão compatível, restauração com a API parada e indicação explícita do resultado antes da retomada (FR-006, FR-008).
- [X] T015 [US3] Disponibilizar manualmente no registro de teste a segunda imagem com uma migração de teste e registrar seu digest em `specs/005-container-installation/quickstart.md` (FR-005, FR-007). Depende de T009.
- [X] T016 [US3] Executar no `specs/005-container-installation/quickstart.md` a atualização entre duas versões de teste, a falha de migração e a restauração em instalação isolada. Verificar o registro sintético e confirmar que a nova API fica indisponível quando a migração falha (FR-005, FR-006, SC-002, SC-003). Baixar do registro a segunda imagem com migração de teste e confirmar a preservação do registro criado na versão inicial. Depende de T012–T014 e T015.

## Fase 6 — Verificação final

- [X] T017 Atualizar `specs/005-container-installation/quickstart.md` com os comandos, pré-requisitos e configuração efetivamente usados para instalar, parar, atualizar e restaurar, incluindo download de imagem identificada (FR-007, FR-008).
- [ ] T018 Executar os checks existentes de backend, integração SQL e Angular de `.github/workflows/ci.yml`. Registrar no `specs/005-container-installation/quickstart.md` os resultados e digests do piloto. Parar os serviços do piloto e verificar que foram encerrados. Depende de T010, T012, T016 e T017.

## Dependências e trabalho em paralelo

Preparação → infraestrutura compartilhada → US1 → validação da US2 → validação da US3 → verificação final. Após a infraestrutura, a US1 pode avançar em paralelo com T011 e T014. A US2 precisa de uma instalação funcional para validar recriação. A US3 precisa dessa instalação e dos volumes persistentes para validar atualização e recuperação.

- **US1** pode avançar em T008 enquanto T011 e T014 são preparados. Na infraestrutura que a habilita, T003, T004 e T005 trabalham em arquivos distintos.
- **US2** pode preparar T011 enquanto a instalação é implementada. T012 aguarda a instalação funcional.
- **US3** pode preparar T014 em paralelo com T013, usando o mesmo contrato de backup. T016 aguarda os dois comandos e a segunda imagem. A preparação das imagens antecede os respectivos testes de download.

## Estratégia de implementação

Demonstrar primeiro a instalação limpa por HTTPS, com migração e cadastro. Em seguida, validar a recriação com dados persistentes e o ciclo de atualização e recuperação. A entrega da spec 005 requer a matriz completa. A publicação automatizada pertence à etapa posterior.
