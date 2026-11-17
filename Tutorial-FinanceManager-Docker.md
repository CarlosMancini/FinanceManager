# FinanceManager — instalação e abertura no Windows

Este documento pode ser enviado para outro computador. Ele reúne um prompt para o Codex e um roteiro para instalar o FinanceManager no Windows. Após a instalação inicial, o arquivo Iniciar-FinanceManager.cmd abre a aplicação por duplo clique, com banco e API executados pelo Docker.

O projeto está em https://github.com/CarlosMancini/FinanceManager.git, na branch codex/conteinerizacao. A imagem atual é privada. A pessoa responsável precisa de permissão de leitura do pacote no GitHub Container Registry. O pacote ZIP de instalação contém os arquivos necessários para executar a aplicação, sem exigir o código-fonte.

## Prompt para usar no outro computador

Copie o bloco completo e envie ao Codex no outro computador, anexando este documento ou colocando-o no workspace.

~~~text
Quero instalar e abrir o FinanceManager localmente no Windows, com frontend, backend e banco iniciados juntos. Também quero preparar os testes das specs 004 e 005 quando necessário. Leia o documento Tutorial-FinanceManager-Docker.md que anexei ou coloquei no workspace. Consulte os artefatos das specs e as instruções AGENTS.md disponíveis antes de agir.

Se recebi o pacote FinanceManager-Windows-Piloto.zip, extraia-o para uma pasta própria e use seus arquivos. O repositório de desenvolvimento é https://github.com/CarlosMancini/FinanceManager.git e a branch é codex/conteinerizacao. Preserve alterações e instalações existentes. Verifique acesso à imagem privada do GHCR, Windows x64, PowerShell 7.5 ou superior, Docker Desktop, WSL 2 e Docker Engine 28 ou superior.

Ajude-me a preparar uma instalação Docker isolada com dados sintéticos usando as imagens por digest deste documento. Oriente-me na autenticação com minha própria conta e na confiança do certificado local. Nunca peça para eu enviar senha, token ou arquivo .env na conversa e não exiba credenciais nos comandos, logs ou respostas.

Execute o Install.ps1, valide o acesso HTTPS e ajude-me com os testes pendentes da spec 005. Use o navegador integrado do Codex para interações no navegador. Solicite minha participação para o teste a partir de outro computador na LAN. Não remova bancos ou volumes existentes. Para um ambiente de teste isolado, use um nome de projeto novo e confirme sua identidade antes de remover seus recursos.

Para a spec 004, prepare o ambiente para cinco participantes reais, cada um com instalação vazia e dados sintéticos. Explique como registrar tempo e resultado. Não represente participantes, não invente medições e não marque a tarefa humana como concluída sem os resultados reais.

Ao terminar, informe o que passou, o que falta e como acessar e parar a aplicação. Encerre apenas processos e contêineres iniciados para o trabalho que não forem mais necessários e preserve os dados necessários aos testes.
~~~

## 1. Pré-requisitos

- Windows x64 com virtualização e WSL 2 habilitados.
- Docker Desktop iniciado em modo de contêineres Linux e Docker Engine 28 ou superior.
- PowerShell 7.5 ou superior, aberto como PowerShell 7 ou pwsh.
- Git, internet e autorização para acessar o repositório e a imagem privada do GHCR.
- Memória e espaço em disco para a aplicação e o SQL Server.

A aplicação foi planejada para Windows x64. O SQL Server em contêiner Linux tem suporte em processadores Intel e AMD x86-64. O instalador usa uma API de certificados disponível no .NET 9 ou posterior. PowerShell 7.5 usa .NET 9 e a versão 7.6 usa .NET 10.

Consulte os [requisitos do Docker Desktop](https://docs.docker.com/desktop/setup/install/windows-install/), o [suporte do SQL Server](https://learn.microsoft.com/sql/linux/install-upgrade/setup), as [versões do PowerShell](https://learn.microsoft.com/powershell/scripting/whats-new/differences-from-windows-powershell) e a [API de certificados](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509certificateloader?view=net-9.0).

Abra o PowerShell 7 e verifique:

~~~powershell
$PSVersionTable.PSVersion
docker version --format '{{.Server.Version}}'
docker compose version
~~~

Se o servidor Docker não responder, abra o Docker Desktop, conclua a configuração inicial e aguarde o mecanismo ficar pronto.

## 2. Obter os arquivos e autenticar no registro

A forma mais simples é extrair FinanceManager-Windows-Piloto.zip em uma pasta própria e abrir o PowerShell 7 nessa pasta. Para trabalhar com o código-fonte, escolha uma pasta de trabalho e clone o repositório:

~~~powershell
git clone --branch codex/conteinerizacao https://github.com/CarlosMancini/FinanceManager.git
cd FinanceManager
~~~

Se o repositório já existir, confira a branch e as alterações locais antes de trocá-la. A raiz necessária contém compose.yaml, .env.example, Iniciar-FinanceManager.cmd e scripts/container. Se a branch remota ainda não incluir o iniciador, use os arquivos do pacote ZIP.

Autentique o Docker no GHCR:

~~~powershell
docker login ghcr.io
~~~

Informe o usuário GitHub e, como senha, um personal access token clássico com escopo read:packages e acesso ao pacote privado. Nunca envie o token ao Codex nem coloque-o no arquivo .env.

## 3. Criar o certificado local

O exemplo cria um certificado para localhost, exporta um PFX com chave privada e adiciona o certificado público às autoridades confiáveis do perfil atual do Windows. Execute apenas para o ambiente que você está preparando.

~~~powershell
$certDir = Join-Path $env:LOCALAPPDATA 'FinanceManager'
New-Item -ItemType Directory -Force -Path $certDir | Out-Null
$certPassword = Read-Host 'Defina uma senha para o PFX' -AsSecureString
$cert = New-SelfSignedCertificate -DnsName localhost -CertStoreLocation Cert:\CurrentUser\My -KeyExportPolicy Exportable -KeySpec KeyExchange
Export-PfxCertificate -Cert $cert -FilePath (Join-Path $certDir 'localhost.pfx') -Password $certPassword
Export-Certificate -Cert $cert -FilePath (Join-Path $certDir 'localhost.cer')
Import-Certificate -FilePath (Join-Path $certDir 'localhost.cer') -CertStoreLocation Cert:\CurrentUser\Root
Join-Path $certDir 'localhost.pfx'
~~~

Anote a senha e o caminho do PFX em local seguro. Para este piloto, o mesmo PFX pode atender ao HTTPS e à proteção das chaves de autenticação. Preserve o PFX e a senha se for manter ou restaurar os dados da instalação.

## 4. Configurar a instalação

Na raiz do projeto, crie o arquivo apenas se ele ainda não existir e abra-o:

~~~powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
notepad .env
~~~

Preencha os valores abaixo. As senhas SQL do exemplo são sintéticas e destinam-se apenas ao piloto. Para uma instalação pessoal, escolha senhas próprias e atualize também as conexões correspondentes. Substitua SEU_USUARIO_WINDOWS e SENHA_DO_PFX. Copie o caminho retornado na etapa anterior, usando barras normais nos dois campos do PFX.

~~~dotenv
COMPOSE_PROJECT_NAME=financemanager-piloto
APP_IMAGE=ghcr.io/carlosmancini/financemanager-pilot@sha256:d7e444b5f26c26289fd00f65b5b0aba1a0ece44a1e5608a41c0e9d1ffd96609d
SQL_IMAGE=mcr.microsoft.com/mssql/server@sha256:05644dad1266ca3d445d1e8f6d2ebb9a6a03df9a39376e0f7b48917356dca9d9
SQL_ADMIN_USER=sa
SQL_ADMIN_PASSWORD=TesteAdmin123!
SQL_MIGRATION_USER=fm_migracao
SQL_MIGRATION_PASSWORD=TesteMigracao123!
SQL_APP_USER=fm_aplicacao
SQL_APP_PASSWORD=TesteAplicacao123!
HTTPS_CERTIFICATE_PATH=C:/Users/SEU_USUARIO_WINDOWS/AppData/Local/FinanceManager/localhost.pfx
HTTPS_CERTIFICATE_PASSWORD=SENHA_DO_PFX
DATA_PROTECTION_CERTIFICATE_PATH=C:/Users/SEU_USUARIO_WINDOWS/AppData/Local/FinanceManager/localhost.pfx
DATA_PROTECTION_CERTIFICATE_PASSWORD=SENHA_DO_PFX
BACKUP_DIRECTORY=C:/Users/SEU_USUARIO_WINDOWS/AppData/Local/FinanceManager/backups
SQL_MIGRATION_CONNECTION_STRING=Server=sql;Database=FinanceManager;User ID=fm_migracao;Password=TesteMigracao123!;Encrypt=True;TrustServerCertificate=True
SQL_APP_CONNECTION_STRING=Server=sql;Database=FinanceManager;User ID=fm_aplicacao;Password=TesteAplicacao123!;Encrypt=True;TrustServerCertificate=True
~~~

As identidades e senhas nas conexões precisam corresponder aos campos da mesma função. O arquivo .env contém credenciais e deve permanecer local, com acesso restrito.

## 5. Primeira instalação e abertura cotidiana

Com Docker Desktop ativo e PowerShell 7 na raiz do projeto:

~~~powershell
./scripts/container/Install.ps1
~~~

O comando obtém as imagens, espera o SQL Server, prepara o banco e as identidades, aplica as migrações e inicia a API com a interface Angular. Quando concluir, abra https://localhost:7043 e confira a opção Criar conta.

Para parar os serviços preservando os volumes:

~~~powershell
./scripts/container/Stop.ps1
~~~

Após essa primeira instalação, dê duplo clique em Iniciar-FinanceManager.cmd na raiz da pasta. O iniciador abre o Docker Desktop se necessário, inicia o banco e a API já instalados e abre o endereço local no navegador padrão. Crie um atalho para esse arquivo na área de trabalho se desejar acesso mais rápido.

O iniciador precisa do mesmo .env e COMPOSE_PROJECT_NAME usados na instalação inicial. Ele usa os contêineres já criados e verifica o resultado da última migração. Preserve os volumes enquanto precisar dos dados da instalação. Para atualizar para uma nova versão, siga o procedimento de atualização no quickstart do projeto.

## 6. Teste humano da spec 004

A T053 verifica SC-001 com cinco participantes reais. Cada participante precisa de uma instalação vazia e deve usar dados sintéticos. Pelo menos quatro precisam criar a conta e entrar em até cinco minutos, sem assistência.

1. Prepare a instalação vazia e confirme que Criar conta está disponível.
2. Explique o objetivo e forneça dados fictícios. A pessoa realiza cadastro e login por conta própria.
3. Meça o tempo desde o início até o login concluído.
4. Registre sucesso ou falha, tempo, eventual assistência e dificuldades. Não registre senhas.
5. Repita com cinco pessoas e conte quantas concluíram em até cinco minutos sem assistência.

Em um mesmo computador, use um COMPOSE_PROJECT_NAME novo por participante, por exemplo financemanager-p01 até financemanager-p05. Pare a instalação anterior antes de mudar o nome no .env e executar novamente o instalador. Cada nome novo cria volumes independentes e começa com banco vazio. Guarde a relação dos nomes usados.

Registre somente resultados observados em specs/004-user-registration/validation-usability.md. O protocolo está em specs/004-user-registration/quickstart.md.

## 7. Testes pendentes da spec 005

A T010 ainda precisa ser concluída. Use uma instalação isolada e vazia e registre:

- Download da imagem pelo digest em computador sem as camadas previamente baixadas.
- HTTPS confiável em localhost, cadastro e login pela interface.
- Rejeição de configuração inválida sem exposição de credenciais.
- Reexecução da instalação com o mesmo projeto e preservação dos dados.
- SQL Server sem porta publicada e aplicação publicada apenas em 127.0.0.1.
- Tentativa de acesso à porta 7043 a partir de outro computador na mesma rede e recusa da conexão.
- Verificação da mensagem para Docker Engine anterior à versão 28, conforme a matriz de validação.

Para conferir serviços e portas na raiz do projeto:

~~~powershell
docker compose ps
~~~

Se não houver outro computador na LAN, registre essa parte como pendente. O roteiro completo de atualização, backup e restauração está em specs/005-container-installation/quickstart.md. A T018 depende das verificações da T010.

## 8. Resolução de problemas

- Se Docker não responder, confirme instalação e inicialização do Docker Desktop.
- Se o GHCR rejeitar o download, confira o login e a permissão de leitura do pacote privado.
- Se o certificado for rejeitado, confira caminho PFX, senha, validade, nome localhost e confiança do certificado público no perfil.
- Se o SQL não iniciar, confira a senha administrativa, os recursos disponíveis no Docker Desktop e as mensagens do serviço.
- Se a porta 7043 estiver ocupada, identifique o processo responsável e preserve sessões de outras pessoas.
- Se o cadastro não estiver disponível, confira a imagem e se a instalação está vazia.

Ao pedir ajuda, remova senhas, tokens, conteúdo do .env e dados pessoais da mensagem de erro.
