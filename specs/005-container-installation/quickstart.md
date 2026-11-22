# Validação da instalação Docker

## Preparação da instalação

No Windows x64, instale o Docker Desktop com WSL 2, inicie contêineres Linux e use Docker Engine 28 ou superior e PowerShell 7. Execute os comandos a partir da raiz do pacote, onde estão compose.yaml, .env.example e scripts/container. O computador precisa de acesso à internet e de permissão de leitura da imagem privada do GHCR. Autentique o Docker no GHCR com sua própria credencial antes da instalação.

Crie a configuração local a partir do modelo. Mantenha o arquivo fora do compartilhamento e restrinja seu acesso, pois ele contém senhas.

~~~powershell
Copy-Item .env.example .env
~~~

Preencha os campos do arquivo .env.

- COMPOSE_PROJECT_NAME identifica a instalação e seus volumes. Preserve o mesmo valor em reinicializações e atualizações.
- APP_IMAGE e SQL_IMAGE recebem as referências por digest da versão desejada. As referências do piloto estão abaixo.
- SQL_ADMIN_PASSWORD é a senha da conta administrativa sa do SQL Server. SQL_MIGRATION_USER e SQL_MIGRATION_PASSWORD identificam a conta que altera o esquema. SQL_APP_USER e SQL_APP_PASSWORD identificam a conta usada pela aplicação.
- SQL_MIGRATION_CONNECTION_STRING e SQL_APP_CONNECTION_STRING são conexões SQL completas que apontam para o serviço sql e o banco FinanceManager. Use as mesmas credenciais correspondentes aos campos anteriores e configure TrustServerCertificate=True para o SQL local. Use a sintaxe de escape de conexão SQL para valores com caracteres especiais.
- HTTPS_CERTIFICATE_PATH e DATA_PROTECTION_CERTIFICATE_PATH apontam para arquivos PFX acessíveis no Windows. Os campos PASSWORD correspondentes contêm as respectivas senhas. BACKUP_DIRECTORY aponta para o diretório local destinado aos backups.

Um exemplo de estrutura para a conexão de migração é Server=sql;Database=FinanceManager;User ID=USUARIO_MIGRACAO;Password=SENHA_MIGRACAO;Encrypt=True;TrustServerCertificate=True. A conexão da aplicação usa o respectivo usuário e senha. Substitua os marcadores por valores próprios.

A instalação precisa de um certificado HTTPS válido para localhost e confiável no perfil do usuário. Também precisa de um PFX com chave privada para proteger as chaves de autenticação. Para um piloto local, o mesmo PFX pode atender aos dois campos. O exemplo abaixo cria um certificado local e o confia no perfil atual. A senha solicitada será necessária nos campos de certificado do arquivo .env.

~~~powershell
$certDir = Join-Path $env:LOCALAPPDATA 'FinanceManager'
New-Item -ItemType Directory -Force -Path $certDir | Out-Null
$certPassword = Read-Host 'Senha do PFX' -AsSecureString
$cert = New-SelfSignedCertificate -DnsName localhost -CertStoreLocation Cert:\CurrentUser\My -KeyExportPolicy Exportable -KeySpec KeyExchange
Export-PfxCertificate -Cert $cert -FilePath (Join-Path $certDir 'localhost.pfx') -Password $certPassword
Export-Certificate -Cert $cert -FilePath (Join-Path $certDir 'localhost.cer')
Import-Certificate -FilePath (Join-Path $certDir 'localhost.cer') -CertStoreLocation Cert:\CurrentUser\Root
~~~

Guarde o PFX de proteção de chaves e sua senha junto ao procedimento de recuperação. O mesmo certificado precisa estar disponível quando forem reutilizadas as chaves persistidas.

## Instalação, parada e recuperação

Para instalar e abrir a aplicação:

~~~powershell
docker version --format '{{.Server.Version}}'
docker compose version
./scripts/container/Install.ps1
~~~

Acesse https://localhost:7043, crie a conta e entre. Para parar os serviços preservando os volumes:

~~~powershell
./scripts/container/Stop.ps1
~~~

Para atualizar, ajuste APP_IMAGE no arquivo .env para o digest da nova versão e execute o comando. O instalador obtém as imagens, para a API e cria um backup verificado antes da migração. Cada execução de atualização cria um backup, mesmo que não haja migração pendente. O caminho do arquivo é mostrado no resultado e um arquivo .json ao lado registra a versão anterior e o checksum. Mantenha o SQL_IMAGE igual ao da instalação existente.

~~~powershell
./scripts/container/Update.ps1
~~~

Para restaurar, use o arquivo .bak e mantenha o arquivo .bak.json correspondente no mesmo diretório. Consulte a imagem anterior nos metadados, obtenha-a no registro e configure APP_IMAGE no .env com essa referência. Mantenha os certificados e as senhas da instalação disponíveis.

~~~powershell
$backup = 'C:\caminho\para\o-backup.bak'
$metadata = Get-Content "$backup.json" -Raw | ConvertFrom-Json
docker pull $metadata.image
./scripts/container/Restore.ps1 -BackupPath $backup
~~~

A restauração deixa a API parada. Após verificar o resultado e o banco restaurado, inicie a versão anterior:

~~~powershell
./scripts/container/Install.ps1
~~~

## Matriz de validação

Para o piloto, use dados sintéticos, um projeto Compose exclusivo e duas versões de teste, com migração na segunda.

No navegador, abrir `https://localhost:7043`, criar conta, entrar e cadastrar um registro sintético. Validar o certificado e tentar acesso a partir de outro computador pela LAN. Confirmar acesso local e recusa externa. Conferir que a porta SQL não foi publicada.

| Caso | Ação | Resultado esperado |
| --- | --- | --- |
| Instalação limpa (SC-001, SC-004) | Executar o fluxo acima com volume SQL vazio e sem a imagem inicial no cache local, baixando-a do registro de teste | Banco e identidades provisionados, migrações aplicadas, HTTPS disponível e cadastro concluído. Reexecutar no mesmo volume e confirmar os dados preservados. Confirmar que a credencial da API opera e que o SQL rejeita uma alteração de esquema feita por ela |
| Recriação (SC-002) | Executar `./scripts/container/Stop.ps1`, remover contêineres do piloto com `docker compose down` sem remover volumes e executar `./scripts/container/Install.ps1` | Conta e registro continuam acessíveis |
| Atualização (SC-002) | Selecionar a segunda imagem por digest, baixá-la do registro de teste e executar `./scripts/container/Update.ps1` | Backup produzido, migração aplicada e registro preservado |
| Falha (FR-004 a FR-006) | Em instalação isolada, testar certificado inválido, SQL indisponível, falha de migração e Engine abaixo de 28. No caso do Engine incompatível, confirmar a mensagem específica em português e a interrupção antes de iniciar os serviços | Mensagem acionável, saída diferente de zero e nova versão indisponível |
| Recuperação (SC-003) | Em instalação isolada, selecionar versão compatível e executar `./scripts/container/Restore.ps1 -BackupPath <arquivo>`, depois iniciar a versão recuperada | Consulta retorna o registro salvo antes da atualização |

Executar os checks de build e testes de backend e Angular da CI. Antes do piloto, registrar as versões e os digests das duas imagens da aplicação e a referência fixada do SQL Server. Durante o piloto, registrar o resultado por caso e a evidência da restauração. Ao terminar, executar `./scripts/container/Stop.ps1` e confirmar que `docker compose ps --status running` não lista serviços do piloto.

## Imagens do piloto inicial

- Aplicação: `ghcr.io/carlosmancini/financemanager-pilot@sha256:d7e444b5f26c26289fd00f65b5b0aba1a0ece44a1e5608a41c0e9d1ffd96609d`.
- SQL Server: `mcr.microsoft.com/mssql/server@sha256:05644dad1266ca3d445d1e8f6d2ebb9a6a03df9a39376e0f7b48917356dca9d9`.

## Evidências parciais da instalação inicial

Em 2026-11-14, um projeto Compose isolado com dados sintéticos iniciou o SQL Server, aplicou as migrações e iniciou a API com a imagem do piloto. A porta HTTPS foi publicada em 127.0.0.1 e o SQL Server não teve porta publicada. Uma senha incorreta de certificado foi rejeitada antes da inicialização. Uma verificação com resposta simulada do Docker Engine 27 confirmou a mensagem de versão mínima.

O navegador integrado rejeitou o certificado de teste por não confiar na autoridade emissora. Permanecem pendentes a verificação de HTTPS com certificado confiável, o cadastro pela interface, a instalação sem camadas em cache e a tentativa a partir de outro computador na LAN. T010 permanece aberta.

## Evidência da preservação após recriação

Em 2026-11-14, um projeto Compose isolado foi instalado com dados sintéticos. Após cadastro, login e criação de uma conta financeira pela API, o comando de parada interrompeu os serviços. Os contêineres foram removidos sem remover os volumes e a instalação foi executada novamente. O login e a conta financeira continuaram disponíveis, os arquivos de chaves mantiveram os mesmos hashes e a quantidade de migrações aplicadas permaneceu igual. Os contêineres e volumes de teste foram removidos ao final.

## Imagem de teste da atualização

A segunda imagem do piloto, usada somente para testar migração, é ghcr.io/carlosmancini/financemanager-pilot@sha256:129a4d6165798d170598b10b11743c063b5a2f604f70e402e6a14d9e53576cce. Ela contém uma migração sintética de validação.

## Evidência de atualização e recuperação

Em 2026-11-14, uma instalação isolada com conta financeira sintética foi atualizada para a imagem de teste. O backup anterior foi criado fora do volume do banco e protegido por permissões de arquivo. A migração sintética foi aplicada e a conta continuou acessível. Uma migração com configuração inválida falhou e a API permaneceu parada. A restauração do backup com a imagem anterior recuperou a conta e o esquema anterior. Os testes também rejeitaram checksum incorreto, imagem anterior incompatível e troca da imagem SQL antes do backup. Os recursos e dados sintéticos foram removidos ao final.


## Checks de verificação final

Em 2026-11-14, a restauração de pacotes com lock file e o build Release da solução passaram. O NuGet retornou NU1900 porque não foi possível consultar os dados de vulnerabilidades. Passaram 98 testes unitários e 80 testes de integração com SQL Server isolado. O build Angular e 50 testes do frontend passaram em contêiner temporário.

A T018 permanece aberta enquanto a T010 estiver pendente. Os testes de cadastro pela interface com certificado confiável, instalação sem camadas em cache e recusa a partir de outro computador na LAN ainda precisam ser registrados.

### Retomada da T018 em 2026-11-22

Verificação da revisão `c04a65c`, seguindo os comandos de `.github/workflows/ci.yml`, sem publicação de imagens ou alteração do workflow. Backend executado no Windows x64 com SDK .NET 10.0.403; integração com Docker Engine 29.9.3 em modo Linux e SQL Server 2025 Developer isolado, publicado apenas em `127.0.0.1:1433`. Como o host não disponibilizava npm no PATH, os checks do frontend foram executados em contêiner temporário com Node 24.22.2, conforme `.node-version`. A execução local não substitui uma execução do workflow no runner Ubuntu 24.04.

| Check da CI | Resultado observado |
| --- | --- |
| `dotnet restore FinanceManager.slnx --locked-mode` | Aprovado, sem aviso NU1900 nesta execução |
| `dotnet build FinanceManager.slnx --configuration Release --no-restore` | Aprovado, zero avisos e zero erros; adicionado `--disable-build-servers` para evitar servidores persistentes |
| `dotnet test --project tests/FinanceManager.UnitTests/FinanceManager.UnitTests.csproj --configuration Release --no-build` | 117 aprovados, zero falhas e zero ignorados |
| `dotnet test --project tests/FinanceManager.IntegrationTests/FinanceManager.IntegrationTests.csproj --configuration Release --no-build` | 96 aprovados, zero falhas e zero ignorados; SQL real após consulta autenticada de prontidão, com senha aleatória somente no ambiente do processo |
| `npm ci` em `src/frontend` | Aprovado; 286 pacotes instalados, 287 auditados; reportadas duas vulnerabilidades (uma moderada e uma crítica) e avisos de scripts de instalação sem aprovação em `allowScripts` |
| `npm run build` em `src/frontend` | Aprovado; saída em `dist/finance-manager` |

O workflow atual não executa `npm test`; os 50 testes frontend registrados em 2026-11-14 são evidência histórica, não uma nova execução. Nenhuma dependência foi alterada para tratar os avisos do npm; a avaliação e correção das vulnerabilidades permanece fora desta retomada.

#### Digests conferidos

As referências abaixo foram conferidas com `docker image inspect` no cache local. Esta conferência não representa um novo download sem cache nem uma nova execução dos cenários do piloto.

| Uso | Referência imutável |
| --- | --- |
| Piloto inicial | `ghcr.io/carlosmancini/financemanager-pilot@sha256:d7e444b5f26c26289fd00f65b5b0aba1a0ece44a1e5608a41c0e9d1ffd96609d` |
| Piloto de atualização com migração sintética | `ghcr.io/carlosmancini/financemanager-pilot@sha256:129a4d6165798d170598b10b11743c063b5a2f604f70e402e6a14d9e53576cce` |
| SQL do piloto e digest resolvido por `2025-latest` na integração desta retomada | `mcr.microsoft.com/mssql/server@sha256:05644dad1266ca3d445d1e8f6d2ebb9a6a03df9a39376e0f7b48917356dca9d9` |
| Node usado nos checks do frontend (`node:24.22.2-bookworm-slim`) | `node@sha256:01d566e8d94d8c619591d78652babfccd3c6992a3dffa9ed5ab39e11cf6c70e9` |

#### Encerramento verificado

No início, `docker ps -a` não listou contêineres de nenhuma instalação. Não havia `.env` local para executar `Stop.ps1`; nenhum piloto Compose foi iniciado nesta retomada. A consulta `docker compose ps` foi impedida pelas variáveis obrigatórias ausentes; a verificação foi feita diretamente no Engine, incluindo o filtro `label=com.docker.compose.project`, que não retornou contêineres.

O contêiner `fm005-t018-sql` foi removido no bloco `finally` da integração; `fm005-t018-frontend` terminou com remoção automática (`--rm`). Antes de parar o Engine, `docker ps -a` e os filtros por nome da validação e por projeto Compose retornaram vazios. O Docker Desktop iniciado para estes checks foi parado com `docker desktop stop`. A consulta dos PIDs registrados confirmou o encerramento do Desktop, seus processos associados e dos processos .NET da validação. As conexões de teste a `127.0.0.1:1433` e `127.0.0.1:7043` retornaram `False` após a limpeza.

Os processos .NET preexistentes do VS Code e seus filhos MSBuild foram preservados. Não foi executado `dotnet build-server shutdown` para evitar interferência nessa sessão; o build desta tarefa usou `--disable-build-servers` e não deixou servidores próprios ativos.

#### Situação após os checks da T018

Na ocasião dos checks, a T018 permanecia aberta por depender da T010. As evidências posteriores e a conclusão da T010 estão registradas abaixo. A verificação de Engine 27 foi simulada. O checklist de requisitos mantém a revisão e aprovação da especificação pendente (quatro itens marcados e um aberto). Não houve avanço para publicação automatizada.

### Evidências adicionais da T010

Em 2026-11-22, uma instalação isolada com volume SQL novo iniciou e foi reinstalada no mesmo volume. A credencial limitada da API consultou o histórico de cinco migrações e recebeu erro de permissão ao tentar criar uma tabela. O SQL não teve porta publicada e a aplicação publicou `127.0.0.1:7043`. Uma requisição HTTPS local com validação de certificado retornou HTTP 200. O usuário confirmou que, a partir de um celular na rede, a tentativa de abrir o IP do host na porta 7043 não conseguiu conectar; não foi realizado teste de controle para excluir isolamento geral entre dispositivos.

O navegador integrado rejeitou o certificado por autoridade não confiável, impedindo o cadastro pela interface. As imagens já estavam em cache; não se validou download sem cache. O piloto foi parado e seus contêineres, volumes, configuração e certificado temporário removidos. A T010 permanece aberta pelos cenários ainda não comprovados, incluindo cadastro pela interface e download sem cache.

Na retomada seguinte em 2026-11-22, um novo volume SQL vazio e o certificado de desenvolvimento já confiável no Windows permitiram abrir a interface HTTPS no navegador integrado. Uma conta sintética foi criada pela interface; após reexecutar a instalação no mesmo volume, o login com essa conta chegou à página de contas. Os serviços, volumes e configuração temporários foram removidos, e o Docker Desktop iniciado para o teste foi encerrado. As imagens ainda estavam em cache: o download e a instalação em ambiente sem cópia local permanecem pendentes. A tentativa pela LAN registrada acima não incluiu controle de conectividade entre os dispositivos. A T010 permanece aberta.

Na verificação posterior em 2026-11-22, o Skopeo em contêiner temporário autenticou no GHCR com a credencial existente e copiou a imagem inicial por digest para um diretório OCI vazio no próprio contêiner, transferindo suas 11 camadas e a configuração sem utilizar o cache de imagens do Docker. A origem solicitada foi `ghcr.io/carlosmancini/financemanager-pilot@sha256:d7e444b5f26c26289fd00f65b5b0aba1a0ece44a1e5608a41c0e9d1ffd96609d`; a cópia concluiu com sucesso. O hash do manifesto convertido para OCI foi `sha256:f7eb8ada4f6969c90e4c0fdf2f7d5de374d041091daae2047b4d73fb885fda7d` e não é o digest original do registro. O contêiner e seu armazenamento foram removidos automaticamente e o Docker foi encerrado. Esta evidência demonstra o download sem cache, mas a instalação anterior utilizou imagens já presentes no Engine; portanto, não comprova uma instalação executada em Engine sem cópia local, como exige a T010.

Em nova verificação, um Docker Engine 28.5.2 isolado iniciou sem imagens e baixou todas as camadas da imagem inicial por digest do GHCR. O Engine e sua configuração de autenticação temporária foram removidos após o teste. A instalação completa não foi executada nesse Engine isolado; essa parte da T010 permanece pendente.

Para complementar a tentativa pela LAN, o usuário acessou pelo celular, na mesma rede, uma resposta HTTP sintética servida temporariamente pelo computador na porta 18080. Essa conexão bem-sucedida fornece controle de conectividade para a tentativa anterior à porta 7043, que não conectou. Os testes ocorreram em momentos diferentes. O servidor de controle foi encerrado e a porta 18080 foi verificada como fechada.

Em nova tentativa no Docker Engine 28.5.2 isolado e inicialmente sem imagens, as imagens da aplicação e do SQL foram baixadas por digest. Com comandos equivalentes ao provisionamento, foram criados banco e identidades, aplicadas cinco migrações e iniciada a aplicação; HTTPS retornou HTTP 200 com verificação do certificado e o SQL permaneceu sem porta publicada. O Engine e todos os seus recursos e credenciais temporárias foram removidos. A tentativa não executou `Install.ps1` nesse Engine nem validou o cadastro pela interface nele; o teste completo do instalador em ambiente sem cópia local continua pendente, e a T010 permanece aberta.

### Conclusão da T010

Em 2026-11-22, `Install.ps1` foi executado a partir do Windows com Docker Engine 28.5.2 aninhado, isolado e inicialmente sem imagens ou volumes. O script baixou as imagens da aplicação e SQL pelos digests registrados, provisionou o banco vazio e as identidades e aplicou as migrações. Na primeira execução, a aplicação não iniciou porque o Compose enviou um caminho Windows de certificado ao Engine Linux aninhado. Após configurar `COMPOSE_CONVERT_WINDOWS_PATHS=1` e mapear o caminho no Engine de teste, a reexecução no mesmo volume concluiu com aplicação saudável. Essa adaptação foi necessária para a topologia de teste aninhada; a execução anterior no Docker Desktop do host havia iniciado normalmente.

Um encaminhamento temporário restrito ao loopback permitiu acessar a aplicação pelo navegador integrado com o certificado de desenvolvimento confiável. O cadastro sintético foi concluído pela interface. Depois de nova execução de `Install.ps1` no mesmo volume, o login com a conta cadastrada chegou à página de contas. As evidências anteriores cobrem ausência de porta SQL publicada, recusa pela LAN com controle de conectividade em momento diferente, rejeição de alteração de esquema pela identidade da API, senha incorreta de certificado e verificação simulada de Engine 27. A T010 foi marcada concluída com essas limitações registradas; a execução aninhada não substitui o teste no Docker Desktop nativo, já registrado acima.

Ao final, `Stop.ps1` parou o projeto, os contêineres e volumes sintéticos foram removidos, o Engine aninhado e sua configuração temporária foram eliminados e o Docker Desktop iniciado para o teste foi encerrado. A ausência dos processos e as portas 23755 e 7043 fechadas foram verificadas.

### Conclusão da T018

Com a T010 concluída e as dependências T012, T016 e T017 já marcadas, a T018 foi concluída com os checks e digests registrados acima. Os checks de 2026-11-22 foram executados na revisão `c04a65c`; desde então, as alterações desta retomada se limitaram aos registros da spec. A validação local não equivale à execução do workflow no runner Ubuntu. A aprovação da especificação no checklist continua pendente de revisão pelo responsável e não foi marcada automaticamente.
