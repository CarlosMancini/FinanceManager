# Especificação: instalação local em contêineres

**Branch**: `codex/conteinerizacao`
**Criada**: 2026-11-14
**Status**: Rascunho
**Entrada**: Preparar instalação local do FinanceManager em Docker para distribuição pela internet.

## Clarifications

### Session 2026-11-14

- Q: A primeira versão em Docker precisa permitir que quem já usa o FinanceManager com LocalDB leve seus dados para a instalação em contêineres? → A: A primeira instalação Docker começa com um banco vazio. A preservação de dados vale para atualizações de instalações Docker existentes.

## Cenários e testes

### Instalação inicial (P1)

O titular instala a aplicação em um computador compatível e a acessa localmente por HTTPS.

**Teste independente**: Em uma instalação limpa, preparar a configuração, iniciar os serviços e criar uma conta pela interface.

1. Dado um computador com os pré-requisitos, quando o titular configurar e iniciar a instalação, então a interface estará acessível localmente por HTTPS e permitirá criar a conta.
2. Dada uma configuração ausente ou inválida, quando iniciar a instalação, então o erro deverá indicar a correção necessária.

### Preservação dos dados (P1)

O titular mantém seus dados após parar, iniciar ou recriar os serviços.

**Teste independente**: Registrar dados sintéticos, recriar os serviços e verificar os dados e o acesso.

1. Dados registros existentes, quando os serviços forem recriados, então os registros continuarão disponíveis.
2. Dado um banco novo, quando a instalação iniciar, então as migrações necessárias serão aplicadas antes do uso.

### Atualização e recuperação (P2)

O titular consegue preparar uma atualização preservando a possibilidade de recuperar seus dados.

**Teste independente**: Atualizar uma instalação Docker anterior com dados sintéticos e restaurar um backup em ambiente de teste.

1. Dada uma instalação Docker anterior, quando atualizada, então seus registros permanecerão acessíveis.
2. Dada uma falha de migração, quando a atualização falhar, então a nova aplicação não deverá ser disponibilizada como atualização bem-sucedida.

### Casos limite

- Banco indisponível durante a inicialização ou migração.
- Certificado inválido, porta ocupada ou credenciais ausentes.
- Interrupção durante a atualização e restauração de backup.

## Requisitos

- **FR-001**: A instalação DEVE disponibilizar interface e API localmente por HTTPS.
- **FR-002**: O acesso à aplicação DEVE permanecer restrito ao computador local.
- **FR-003**: O banco e as chaves de autenticação DEVEM persistir após recriação dos serviços.
- **FR-004**: A instalação DEVE configurar credenciais e certificados fora do repositório e das imagens distribuídas.
- **FR-005**: A instalação DEVE aplicar as migrações necessárias após o banco estar pronto e antes de disponibilizar a aplicação.
- **FR-006**: A atualização de uma instalação Docker existente DEVE preservar os dados e fornecer procedimento de backup e restauração.
- **FR-007**: A distribuição DEVE permitir obter as imagens pela internet e executar uma versão identificada.
- **FR-008**: As instruções DEVEM cobrir instalação, inicialização, parada, atualização e recuperação.

## Critérios de sucesso

- **SC-001**: Em uma instalação limpa, o titular consegue abrir a interface e criar uma conta.
- **SC-002**: Registros sintéticos permanecem disponíveis após recriar os serviços e após atualizar a versão.
- **SC-003**: Um backup de teste é restaurado e seus registros podem ser consultados.
- **SC-004**: A interface é acessível localmente por HTTPS durante a validação.

## Premissas

- A primeira plataforma alvo é Windows x64 com mecanismo Docker e acesso à internet.
- Cada computador possui seus próprios dados e conta local.
- A primeira instalação Docker começa com um banco vazio. A transferência de dados do LocalDB fica fora do escopo da fase 1.
- A publicação automatizada das releases será tratada em etapa posterior.
