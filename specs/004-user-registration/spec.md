# Feature Specification: Cadastro de usuário e acesso por e-mail

**Feature Branch**: `docs/cadastro-usuario`

**Created**: 2026-11-12

**Status**: Revisão de 2026-11-16 autorizada para implementação

**Input**: Cadastro com nome completo, apelido opcional, data de nascimento, e-mail, telefone e senha segura. Login por e-mail e senha como tela inicial, com opção de criar conta. Recuperação de senha fica para depois.

## Clarifications

### Session 2026-11-12

- Q: Como tratar a instalação que já tem um titular provisionado e dados financeiros? → A: Permitir ao titular existente completar o cadastro, mantendo sua identidade e dados financeiros, e incluir a transição para login por e-mail nesta feature (opção B).
- Q: Enquanto o titular antigo ainda não completou o cadastro, o que deve acontecer ao entrar com o nome de acesso e a senha atuais? → A: Validar as credenciais antigas e encaminhar obrigatoriamente à conclusão do cadastro, sem acesso financeiro (opção A).
- Q: Por quanto tempo a comprovação das credenciais antigas deve permitir concluir o cadastro sem pedir a senha novamente? → A: Dez minutos após a comprovação, sem renovação automática; após a expiração, exigir as credenciais antigas novamente, preservando os dados não secretos preenchidos (opção A).

### Session 2026-11-16

- Revisão aprovada: múltiplos usuários locais com dados financeiros isolados; um login por e-mail normalizado. Criar conta permanece disponível. Duplicidade é informada no envio, sem alterar conta existente.
- Confirmação de senha obrigatória, com ambas válidas e exatamente iguais; telefone com 11 dígitos e máscara `(61) 99843-3486`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Criar a conta do titular (Priority: P1)

Como pessoa que acessa uma instalação nova ou com outros usuários, quero escolher **Criar conta** na tela inicial de login e informar meus dados e uma senha para obter acesso pessoal.

**Why this priority**: Permite iniciar o uso pela interface, sem provisionamento manual.

**Independent Test**: Em instalação sem titular, abrir a aplicação, acessar o cadastro, preencher os dados válidos e confirmar o sucesso e o retorno ao login. Verificar a persistência após reiniciar a aplicação.

**Acceptance Scenarios**:

1. **Given** uma instalação nova ou com outros titulares e uma pessoa sem sessão, **When** abre a aplicação, **Then** vê o login com e-mail, senha e **Criar conta**; essa opção abre a tela de cadastro, com opção de voltar ao login. (FR-001–002)
2. **Given** o formulário aberto, **When** informa todos os dados obrigatórios válidos, sem apelido, **Then** cria uma única conta, recebe confirmação e retorna ao login sem autenticação automática. Se informar apelido, ele é salvo; apelido vazio ou só com espaços é tratado como ausente. (FR-003–008, FR-010)
3. **Given** qualquer campo obrigatório ausente ou dado inválido, **When** tenta concluir, **Then** recebe orientação em português junto ao campo e nenhuma conta é criada; pode corrigir os dados não secretos sem preenchê-los novamente. (FR-003–007, FR-014)
4. **Given** senha com menos de 12 ou mais de 128 caracteres, ou sem qualquer categoria exigida, **When** tenta concluir, **Then** vê os critérios não atendidos e o cadastro é rejeitado. Senhas de 12 e de 128 caracteres que atendam às categorias são aceitas. (FR-007)
5. **Given** outros titulares ou cadastros simultâneos, **When** cadastra e-mails diferentes, **Then** cada conta é criada com identidade e dados financeiros isolados. Para o mesmo e-mail normalizado, somente um cadastro é aceito; no envio, o outro recebe aviso de e-mail já registrado, sem substituição. (FR-005, FR-008, FR-012)
6. **Given** falha ou interrupção durante o cadastro, **When** volta a tentar, **Then** não existem cadastros parciais ou duplicados; só há sucesso após confirmação da gravação. Se o envio anterior foi concluído, a interface orienta a fazer login. (FR-008, FR-014)
7. **Given** senha e confirmação ausentes, inválidas ou diferentes, **When** tenta cadastrar, **Then** recebe orientação no campo correspondente e nenhuma conta é criada; duas senhas válidas e exatamente iguais permitem prosseguir. (FR-007, FR-018)
8. **Given** o campo de telefone, **When** digita os números `61998433486`, **Then** vê `(61) 99843-3486`; ao enviar menos ou mais de 11 dígitos, recebe orientação e nenhuma conta é criada. (FR-006)

---

### User Story 2 - Entrar com e-mail e senha (Priority: P1)

Como titular cadastrado, quero entrar com meu e-mail e senha e retomar minhas operações financeiras.

**Why this priority**: Torna utilizável a conta e preserva o acesso protegido às funcionalidades existentes.

**Independent Test**: Com um titular cadastrado como preparação, validar login correto, rejeição de credenciais incorretas, saída e expiração, sem executar o cadastro durante o teste.

**Acceptance Scenarios**:

1. **Given** cadastro concluído, **When** informa e-mail e senha corretos, **Then** entra no histórico de movimentações se já possui contas financeiras ou no cadastro de conta financeira se ainda não possui nenhuma, conforme MOV-05. (FR-009, FR-011)
2. **Given** o e-mail cadastrado, **When** entra com variações de maiúsculas/minúsculas ou espaços nas extremidades do e-mail, **Then** acessa a mesma identidade. A senha é comparada exatamente como definida. (FR-005, FR-007, FR-009)
3. **Given** senha incorreta ou e-mail não cadastrado, **When** tenta entrar, **Then** não recebe acesso e vê a mesma mensagem genérica em português, sem indicação de qual credencial existe. Tentativas repetidas recebem a proteção já existente na fundação. (FR-009, FR-013)
4. **Given** sessão ausente, revogada ou expirada, **When** tenta acessar uma área financeira, **Then** precisa fazer login; ao sair ou completar 30 minutos sem atividade autenticada, perde o acesso conforme FND-01. (FR-012)
5. **Given** cadastro concluído, **When** tenta entrar apenas com o antigo nome de acesso, **Then** o acesso é rejeitado; o identificador de login passa a ser o e-mail. (FR-009)
6. **Given** cadastro concluído e sessão ainda válida, **When** reabre a aplicação, **Then** segue a navegação autenticada existente sem encerrar a sessão apenas por reabrir a aplicação. (FR-001)

---

### User Story 3 - Completar o cadastro do titular existente (Priority: P1)

Como titular provisionado antes desta feature, quero completar meus dados e definir o e-mail e a senha de acesso sem perder minhas contas, movimentações ou pagamentos.

**Why this priority**: A transição aprovada precisa preservar a identidade e o histórico de quem já usa a aplicação.

**Independent Test**: Preparar uma instalação com titular provisionado e histórico sintético. Concluir a transição, entrar com o e-mail e a nova senha e comparar identidade, vínculos e histórico antes e depois.

**Acceptance Scenarios**:

1. **Given** titular provisionado com cadastro incompleto, **When** acessa **Criar conta**, **Then** encontra a opção de completar o cadastro existente, que exige comprovar o nome de acesso e a senha atuais antes de permitir a conclusão. (FR-015)
2. **Given** comprovação válida, **When** informa todos os dados obrigatórios e uma senha conforme FR-007, **Then** atualiza o mesmo titular, preserva seu identificador e todo o histórico financeiro e retorna ao login para entrada por e-mail. (FR-003–007, FR-010, FR-015–016)
3. **Given** credenciais atuais inválidas ou ausência de comprovação, **When** tenta completar o cadastro, **Then** não obtém acesso nem altera o titular. Conhecer apenas seu e-mail, nome ou telefone não autoriza a operação. Tentativas repetidas recebem a proteção aplicada ao login. (FR-013, FR-015)
4. **Given** falha na conclusão, **When** a operação termina sem sucesso, **Then** cadastro e credenciais anteriores permanecem utilizáveis para repetir a transição, sem atualização parcial. (FR-014–016)
5. **Given** transição concluída, **When** tenta reutilizar credenciais ou autorização de transição anteriores, **Then** não consegue alterar novamente o cadastro; sessões anteriores também não concedem acesso. (FR-016)
6. **Given** duas conclusões simultâneas, **When** ambas tentam salvar, **Then** somente uma é efetivada e a outra não sobrescreve seus dados ou credenciais. (FR-016)
7. **Given** titular provisionado com cadastro incompleto, **When** informa nome de acesso e senha atuais no login, **Then** a comprovação válida o encaminha obrigatoriamente à conclusão do cadastro, sem conceder acesso financeiro. (FR-015)
8. **Given** credenciais antigas comprovadas, **When** tenta concluir o cadastro antes de dez minutos, **Then** a autorização permite a conclusão; ao completar dez minutos, a conclusão é recusada até nova comprovação, preservando os dados não secretos preenchidos. Atividade durante o preenchimento não renova esse prazo. (FR-017)

### Edge Cases

- Campos obrigatórios só com espaços são ausentes. Nomes com acentos, hífens e apóstrofos são aceitos, sem exigir artificialmente dois sobrenomes.
- Data inexistente, como 31/02, ou futura é rejeitada. Hoje é aceito; não há idade mínima nesta feature.
- E-mail sem parte local, sem domínio, com espaços internos ou sem um único `@` é rejeitado.
- Telefone deve conter exatamente 11 dígitos ASCII, incluindo DDD. A interface aceita entrada numérica e aplica `(DD) XXXXX-XXXX`; servidor aceita dígitos ou essa formatação. Entradas incompletas, excedentes e símbolos não permitidos são rejeitados.
- Cada categoria da senha é verificada separadamente, inclusive nos limites de comprimento; senha não é aparada, truncada nem tem sua caixa alterada.
- Retornar do cadastro ao login não cria conta. Senhas não permanecem preenchidas ao abandonar a tela ou após sucesso.
- Dois cadastros simultâneos com o mesmo e-mail normalizado criam somente uma conta; e-mails diferentes podem criar contas independentes.
- Falha ou tentativa sem comprovação das credenciais antigas não pode reivindicar o titular existente, mesmo na instalação local.
- A autorização de transição expira ao completar dez minutos desde a comprovação, inclusive durante o preenchimento. A conclusão após esse limite exige nova comprovação das credenciais antigas, preservando os dados não secretos preenchidos.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A aplicação DEVE apresentar o login como tela inicial para pessoas sem sessão válida, inclusive após expiração ou saída. Sessões válidas de titular com cadastro concluído seguem a navegação autenticada existente; cadastro incompleto segue a conclusão obrigatória de FR-012 e FR-015.
- **FR-002**: O login DEVE oferecer **Criar conta**, que abre tela dedicada de cadastro com opção de voltar ao login. O formulário permanece disponível mesmo com contas existentes; a transição pendente é uma opção separada.
- **FR-003**: O cadastro DEVE solicitar nome completo, apelido/nome pelo qual deseja ser chamado, data de nascimento, e-mail, telefone, senha e confirmação de senha. Apenas o apelido é opcional. Campos obrigatórios ausentes ou só com espaços DEVEM impedir a conclusão, inclusive em tentativas fora da interface.
- **FR-004**: O sistema DEVE salvar nome e apelido preservando a grafia, removendo somente espaços nas extremidades. Apelido vazio é ausente. O nascimento DEVE ser uma data civil existente e não futura, avaliada em America/Sao_Paulo, sem idade mínima.
- **FR-005**: O e-mail DEVE conter parte local e domínio não vazios separados por um único `@`, sem espaços internos. Espaços nas extremidades DEVEM ser ignorados; identificação e login DEVEM ignorar diferenças de maiúsculas/minúsculas. O mesmo e-mail normalizado NÃO DEVE identificar contas diferentes.
- **FR-006**: O telefone DEVE conter exatamente 11 dígitos ASCII, incluindo DDD. A interface DEVE aceitar somente números e completar a máscara `(DD) XXXXX-XXXX`, como `(61) 99843-3486`. A API DEVE aceitar os 11 dígitos ou o formato completo da máscara e normalizar para dígitos. Quantidades diferentes e caracteres inválidos DEVEM produzir orientação em português. Não há verificação por SMS.
- **FR-007**: A senha DEVE conter de 12 a 128 caracteres, pelo menos uma letra maiúscula, uma minúscula, um dígito de `0` a `9` e um símbolo que não seja letra, dígito ou espaço. Para comprimento, cada ponto de código Unicode conta como um caractere. A interface DEVE informar os critérios antes do envio e identificar os não atendidos. Espaços são permitidos, contam no comprimento e não satisfazem o requisito de símbolo. A senha DEVE ser preservada exatamente para autenticação, ocultada por padrão no preenchimento e nunca armazenada de forma recuperável nem exposta em logs ou respostas.
- **FR-008**: O cadastro DEVE permitir novos titulares mesmo com outros cadastros completos ou legados. Antes da gravação, DEVE pesquisar o e-mail normalizado; no envio de e-mail já registrado, rejeitar o cadastro e informar em português que esse e-mail já possui login. A persistência DEVE garantir unicidade inclusive em concorrência, sem alterar dados existentes. Cadastro, credencial e auditoria DEVEM ser atômicos. E-mails diferentes criam titulares independentes.
- **FR-009**: Após concluir o cadastro, o sistema DEVE autenticar exclusivamente pelo e-mail e senha definidos. Nome completo, apelido e antigo nome de acesso NÃO DEVEM funcionar como identificadores alternativos. Credenciais inválidas DEVEM produzir a mesma mensagem genérica, sem conceder sessão.
- **FR-010**: O sucesso do cadastro inicial ou da transição DEVE ser confirmado em português e levar ao login para autenticação explícita, sem login automático e sem transportar a senha para a tela de login.
- **FR-011**: O login bem-sucedido DEVE preservar MOV-05 da feature 002: histórico de movimentações quando existe alguma conta financeira, ou formulário de cadastro de conta financeira quando não existe nenhuma. Conta de usuário e conta financeira são conceitos distintos.
- **FR-012**: O sistema DEVE preservar saída com revogação, expiração após 30 minutos sem atividade autenticada e proteção por titularidade conforme FND-01. Cadastro e transição não DEVEM conceder acesso financeiro sem cadastro e login por e-mail concluídos. Sessões anteriores de titular com cadastro incompleto NÃO DEVEM permitir acesso financeiro; o titular DEVE ser encaminhado à conclusão do cadastro.
- **FR-013**: O sistema DEVE preservar a proteção existente contra tentativas repetidas de autenticação e aplicá-la também à comprovação de credenciais na transição. Cadastro e transição DEVEM manter as proteções constitucionais contra alterações forjadas. Credenciais e dados pessoais do cadastro NÃO DEVEM aparecer em logs. Somente as funções de acesso e cadastro desta feature podem ser usadas sem autenticação financeira.
- **FR-014**: Erros DEVEM ser apresentados em português, identificando campos corrigíveis sem expor detalhes internos. Falha de gravação NÃO DEVE produzir confirmação de sucesso. Erros de validação DEVEM permitir corrigir os dados não secretos sem preenchê-los novamente; senhas DEVEM ser descartadas ao abandonar a tela ou concluir a operação.
- **FR-015**: Para titular provisionado sem cadastro completo, o sistema DEVE permitir completar o cadastro pela interface, mediante comprovação do nome de acesso e senha atuais, tanto pelo login quanto por **Criar conta → Completar cadastro existente**. No login, credenciais antigas válidas DEVEM encaminhar obrigatoriamente à conclusão do cadastro, sem conceder acesso financeiro. A comprovação autoriza somente a conclusão do cadastro daquele titular; não permite criar outro, acessar dados financeiros ou dispensar campos e critérios de FR-003–007. A transição NÃO DEVE estar disponível para titular com cadastro concluído.
- **FR-016**: A transição DEVE preservar identificador, vínculos e todos os registros financeiros do titular, sem criar outra identidade. Dados e novas credenciais DEVEM ser salvos como uma única alteração; falhas preservam o estado anterior. Após sucesso, sessões e autorizações de transição anteriores DEVEM ser invalidadas; somente o novo login por e-mail e senha permite reentrar. Conclusões simultâneas NÃO DEVEM sobrescrever a primeira conclusão efetivada.
- **FR-017**: A autorização para concluir a transição DEVE expirar ao completar dez minutos desde a comprovação das credenciais antigas, sem renovação automática por atividade. O sistema DEVE verificar sua validade ao concluir o cadastro e rejeitar a conclusão com autorização expirada, sem alterar dados ou credenciais persistidos. Após a expiração, DEVE solicitar nova comprovação das credenciais antigas, preservando os dados não secretos preenchidos; uma nova comprovação válida inicia novo prazo de dez minutos.

- **FR-018**: Cadastro e conclusão legada DEVEM exigir confirmação de senha. Os dois campos DEVEM satisfazer FR-007 e ser exatamente iguais, sem trim ou mudança de caixa. Divergência ou ausência DEVE impedir gravação na interface e API e apresentar erro no campo de confirmação. Ambas as senhas DEVEM ser descartadas ao sair ou concluir.

### Key Entities *(include if feature involves data)*

- **Titular / usuário**: Pessoa com identidade própria na instalação, com identificador estável, nome completo, apelido opcional, nascimento, e-mail e telefone. Mantém a titularidade das contas financeiras e do histórico.
- **Credencial de acesso**: E-mail identificador e comprovação protegida da senha, cuja forma original não é recuperável.
- **Sessão**: Acesso autenticado revogável, vinculado ao titular, com as regras de inatividade da fundação.
- **Estado de conclusão do cadastro**: Distingue titular provisionado que precisa completar os dados daquele com login definitivo por e-mail. A transição mantém a identidade.
- **Autorização de transição**: Permissão restrita à conclusão do cadastro do titular comprovado, válida por dez minutos desde a comprovação, sem renovação automática e invalidada após a conclusão.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em avaliação com cinco participantes usando dados sintéticos e instalação vazia por participante, pelo menos quatro conseguem criar a conta e entrar em até cinco minutos, sem assistência, demonstrando clareza do fluxo e das instruções de senha.
- **SC-002**: Em 100% dos cenários de validação previstos, dados obrigatórios ausentes, nascimento inválido/futuro, e-mail/telefone inválidos, senha fora da política ou confirmação inválida/diferente impedem a conclusão; apelido ausente e senhas válidas e iguais nos dois limites são aceitos.
- **SC-003**: Em 100% das tentativas verificadas, cadastro inicial ou transição concluídos permitem login pelo e-mail e senha definidos, inclusive após reiniciar a aplicação, e seguem o destino correto para zero contas ou contas existentes.
- **SC-004**: Em 100% dos cenários de repetição, concorrência e falha, a instalação mantém no máximo um login por e-mail normalizado, permite e-mails distintos e não produz cadastro parcial nem acesso ou alteração de dados de outro titular.
- **SC-005**: Em 100% das transições verificadas com histórico sintético de contas, movimentações, obrigações e pagamentos, identificador do titular, quantidade de registros, vínculos e valores permanecem iguais antes e depois.
- **SC-006**: Em 100% dos cenários de acesso sem sessão, credenciais incorretas, saída, expiração, cadastro incompleto e reutilização de sessões anteriores à transição, o acesso financeiro é recusado com orientação em português. Credenciais antigas válidas no login encaminham à conclusão do cadastro.
- **SC-007**: Nos testes temporais da transição, a autorização permite concluir antes de dez minutos e é rejeitada ao completar esse prazo, mesmo com atividade durante o preenchimento. Em 100% das tentativas com autorização expirada, dados e credenciais persistidos permanecem inalterados, os dados não secretos preenchidos são preservados e somente nova comprovação válida permite repetir a conclusão.

## Assumptions

- A expressão original “sistema de implementação de senha” foi interpretada como recuperação/redefinição de senha. Recuperação, troca posterior de senha, edição posterior de perfil, exclusão de conta, confirmação de e-mail/telefone e autenticação adicional ficam fora do escopo. Definir senha durante a transição integra o cadastro aprovado.
- A versão continua local e permite múltiplos titulares isolados conforme a constituição 2.0.0, sem perfis de permissão ou cadastro público remoto.
- Política de senha, telefone com 11 dígitos, ausência de idade mínima e retorno ao login após cadastro seguem os requisitos aprovados. Não há troca periódica obrigatória de senha nesta feature.
- A tela inicial de login se aplica a quem não tem sessão válida; não exige encerrar sessões válidas a cada abertura. Para titular com cadastro incompleto, a conclusão do cadastro é obrigatória antes de qualquer acesso financeiro, inclusive quando possui sessão anterior.
- Para titular provisionado, as credenciais atuais comprovam identidade. Quem não as possui não consegue concluir a transição, pois recuperação de senha está fora do escopo.
- Dependências: [FND-01 e titularidade da fundação](../001-financial-foundation/spec.md) e [MOV-05 e navegação após login](../002-transactions/spec.md). Esta feature evolui o provisionamento e a identificação de acesso, mantendo regras financeiras e de sessão.
- O plano e os contratos desta feature evoluem o provisionamento antigo para cadastro e login por e-mail. A revisão aprovada em 2026-11-16 substitui a restrição histórica de um titular, conforme a emenda constitucional 2.0.0, preservando titularidade e histórico financeiro.
- A revisão dos artefatos e sua implementação foram autorizadas pelo responsável; a conclusão depende das evidências e tarefas de validação.
