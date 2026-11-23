# Feature Specification: Atualização cadastral do usuário autenticado

**Feature Branch**: `main` (branch atual; nenhuma branch criada nesta etapa)

**Created**: 2026-11-22

**Status**: Draft — aguardando revisão para planejamento

**Input**: Usuário autenticado deve acessar “Atualizar Cadastro” junto a contas a pagar, movimentações e contas; editar os mesmos dados de register; opcionalmente acionar “Alterar senha” para informar senha atual, nova senha e confirmação; validar tudo ao clicar em “Atualizar cadastro”, no final da tela; alterar somente os dados efetivamente modificados.

## Clarifications

### Session 2026-11-22

- Q: Se o cadastro estiver aberto em duas abas e ambas alterarem o mesmo campo, o que deve acontecer quando a segunda aba enviar? → A: Se o mesmo campo mudou desde a abertura, rejeitar a atualização inteira e solicitar revisão dos dados atuais. Alterações em campos diferentes continuam permitidas.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Atualizar os próprios dados cadastrais (Priority: P1)

Como usuário autenticado, quero abrir meu cadastro preenchido e corrigir meus dados sem precisar trocar a senha nem alterar informações que deixei como estavam.

**Why this priority**: Entrega o fluxo principal de manutenção do cadastro com preservação da identidade e do histórico.

**Independent Test**: Com um usuário cadastrado e autenticado, abrir a opção, editar somente o telefone, enviar e reabrir a tela; comparar os demais dados e verificar o acesso com as credenciais anteriores.

**Acceptance Scenarios**:

1. **Given** login bem-sucedido, com ou sem contas financeiras, **When** acessa a navegação autenticada, **Then** encontra “Atualizar Cadastro” ao lado de contas a pagar, movimentações e contas, e essa opção abre uma tela dedicada. (FR-001)
2. **Given** a tela aberta, **When** consulta o formulário, **Then** vê nome completo, apelido, nascimento, e-mail e telefone preenchidos com seus dados, a opção “Alterar senha” e o botão “Atualizar cadastro” no final; nenhuma senha é exibida ou recuperada. (FR-002–003)
3. **Given** alteração válida somente em um campo, **When** envia, **Then** somente esse dado muda, recebe confirmação em português e, ao reabrir, encontra o novo valor; os demais dados, a senha, a identidade e o histórico financeiro permanecem iguais. (FR-004, FR-010–012)
4. **Given** dados obrigatórios ausentes ou inválidos, **When** digita ou sai dos campos e depois envia, **Then** erros são apresentados somente no envio, junto aos campos correspondentes, nenhuma alteração é salva e os dados não secretos preenchidos permanecem disponíveis para correção. (FR-005–007, FR-011–012)
5. **Given** e-mail próprio sem alteração de identidade após normalização, **When** envia, **Then** não recebe erro de duplicidade; se usar e-mail de outro titular, o envio é rejeitado sem alterar qualquer conta. (FR-006, FR-010–011)
6. **Given** apelido existente, **When** o apaga e envia dados válidos, **Then** o apelido é removido; deixar o apelido intacto o preserva. (FR-004–005)
7. **Given** nenhum dado efetivamente alterado, **When** envia, **Then** recebe informação de que não há alterações e nenhum dado ou credencial é regravado. (FR-010, FR-012)
8. **Given** alteração válida de e-mail, **When** conclui e posteriormente faz novo login, **Then** o novo e-mail permite acessar a mesma identidade e o anterior deixa de identificá-la, mantendo a senha caso ela não tenha sido alterada. (FR-006, FR-013)

---

### User Story 2 - Alterar a senha mediante comprovação da senha atual (Priority: P1)

Como usuário autenticado, quero optar por alterar minha senha no mesmo formulário e confirmar essa alteração com minha senha atual.

**Why this priority**: A troca opcional de credencial faz parte do fluxo solicitado e deve impedir atualizações parciais quando a comprovação falhar.

**Independent Test**: Com usuário preparado, abrir a tela, acionar “Alterar senha”, enviar senha atual correta e duas novas senhas válidas e iguais; verificar novo login e rejeição da senha anterior, sem depender de alteração dos dados pessoais.

**Acceptance Scenarios**:

1. **Given** formulário inicialmente aberto, **When** aciona “Alterar senha”, **Then** aparecem exatamente três campos de senha vazios: senha atual, nova senha e confirmação da nova senha; acionar a opção não valida nem salva dados. (FR-003, FR-007–008)
2. **Given** senha atual correta e novas senhas válidas e iguais, **When** clica em “Atualizar cadastro”, **Then** a senha é substituída e os dados pessoais modificados são salvos na mesma operação; novo login aceita a nova senha e rejeita a anterior quando diferentes. (FR-008–009, FR-011, FR-013)
3. **Given** senha atual incorreta ou ausente, **When** envia, **Then** recebe erro em português associado ao campo, e nem senha nem dados pessoais são alterados. A comprovação considera a credencial vigente no momento do envio. (FR-008, FR-011–012)
4. **Given** nova senha ou confirmação ausente, fora da política ou diferente da outra, **When** envia, **Then** os erros correspondentes são apresentados e toda a atualização é rejeitada. Os limites de 12 e 128 caracteres são aceitos quando os demais critérios são atendidos. (FR-009, FR-011–012)
5. **Given** opção “Alterar senha” não acionada, **When** envia dados pessoais válidos, **Then** nenhuma senha é exigida nem alterada. (FR-003–004)

---

### User Story 3 - Preservar acesso restrito e dados em caso de falha (Priority: P1)

Como titular, quero que somente minha sessão possa atualizar meu cadastro e que falhas preservem meus dados e credenciais.

**Why this priority**: A atualização envolve dados pessoais e credenciais de titulares distintos.

**Independent Test**: Preparar dois titulares com históricos sintéticos, tentar acesso sem sessão ou alteração cruzada e simular falha ao salvar; comparar o estado anterior e posterior de ambos.

**Acceptance Scenarios**:

1. **Given** sessão ausente, expirada ou revogada, **When** tenta abrir ou enviar o cadastro, **Then** precisa se autenticar, nenhum dado pessoal é fornecido e nada é alterado. (FR-014)
2. **Given** sessão de um titular, **When** tenta consultar ou alterar o cadastro de outro, **Then** a operação é recusada e ambos mantêm os dados anteriores. (FR-014)
3. **Given** alteração de perfil com troca de senha, **When** ocorre falha de gravação, **Then** todos os dados e credenciais anteriores permanecem, sem mensagem de sucesso. (FR-011–012)
4. **Given** dois titulares tentando adotar o mesmo e-mail normalizado disponível, **When** concluem simultaneamente, **Then** no máximo um o obtém e o outro recebe erro sem atualização parcial. (FR-006, FR-011)
5. **Given** o cadastro aberto em duas abas e uma alteração já salva em um campo que a outra aba também editou, **When** a segunda aba envia, **Then** toda a atualização é rejeitada, inclusive eventual troca de senha; nenhum dado desse envio é salvo, a primeira alteração permanece e o usuário recebe orientação para revisar os dados atuais, preservando o rascunho não secreto. (FR-010–012)
6. **Given** duas abas com alterações em campos pessoais diferentes, **When** ambas enviam dados válidos, **Then** ambas as alterações são preservadas, sem rejeição causada apenas pela edição do campo que aquela aba não modificou. (FR-010–011)

### Edge Cases

- Nome obrigatório só com espaços é inválido; apelido só com espaços significa remoção explícita. Acentos, hífens e apóstrofos são preservados.
- Nascimento inexistente ou futuro é rejeitado; data de hoje é aceita, sem idade mínima.
- E-mail sem extensão, com vírgula, espaços internos, múltiplos `@` ou segmentos de domínio vazios é rejeitado; `.com` e `.com.br` são aceitos. Caixa e espaços externos não tornam o próprio e-mail duplicado.
- Telefone incompleto é inválido; digitação e colagem limitam a entrada aos primeiros 11 dígitos, com a mesma máscara do cadastro. A máscara e os limites de entrada não antecipam mensagens de validação.
- Senha atual é comparada exatamente com a credencial vigente, sem exigir que uma senha antiga satisfaça a política de novas senhas.
- Nova senha e confirmação não são aparadas nem têm caixa alterada; espaços não contam como símbolo. Cada ponto de código Unicode conta como um caractere.
- Sair da tela sem enviar não salva nada e descarta os campos de senha; nenhum campo de senha é preenchido a partir dos dados existentes.
- Falha de carregamento não apresenta formulário vazio como se fosse cadastro existente; orienta repetir a consulta sem permitir sobrescrita acidental.
- Edição em sessões distintas não pode sobrescrever campos que não foram efetivamente modificados no envio em questão. Se algum campo pessoal editado tiver valor vigente diferente do carregado na abertura, considerando as normalizações do cadastro, rejeitar o envio inteiro e solicitar revisão dos dados atuais; alterações apenas em campos não editados não causam esse conflito. A rejeição também se aplica se o valor pretendido coincidir com o novo valor vigente, pois o campo mudou desde a abertura.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A navegação disponível após login bem-sucedido DEVE incluir “Atualizar Cadastro” junto às opções de contas a pagar, movimentações e contas, independentemente da existência de contas financeiras, levando a uma tela dedicada à atualização do próprio cadastro.
- **FR-002**: A tela DEVE carregar os dados atuais do titular autenticado e oferecer os mesmos campos pessoais de register: nome completo, apelido opcional, nascimento, e-mail e telefone. Falha ao carregar DEVE ser informada em português, sem permitir atualização com dados incompletos por falha de consulta.
- **FR-003**: A tela DEVE oferecer “Alterar senha”, inicialmente sem campos de senha visíveis. Acionar essa opção DEVE revelar senha atual, nova senha e confirmação da nova senha, todos vazios e ocultando os caracteres digitados. Nova senha e confirmação correspondem aos dois campos de senha de register. Senha existente nunca DEVE ser apresentada.
- **FR-004**: Sem acionar “Alterar senha”, o sistema DEVE permitir atualizar somente dados pessoais, sem exigir senha atual, nova senha ou confirmação e sem modificar a credencial.
- **FR-005**: Dados pessoais DEVEM seguir as regras aprovadas em FR-003–006 da feature 004: somente apelido é opcional; nome e apelido preservam grafia e removem espaços externos; nascimento deve existir e não ser futuro em America/Sao_Paulo, sem idade mínima; telefone exige 11 dígitos ASCII, máscara `(DD) XXXXX-XXXX`, placeholder `(DDD) + Telefone` e limite de entrada aos primeiros 11 dígitos. Apagar explicitamente o apelido DEVE removê-lo.
- **FR-006**: E-mail DEVE seguir FR-005 da feature 004, com parte local, um único `@`, domínio e extensão separados por ponto, sem segmentos vazios, vírgulas ou espaços internos. A identidade do e-mail ignora caixa e espaços externos. O próprio e-mail DEVE ser aceito; e-mail de outro titular DEVE ser rejeitado no envio, inclusive em concorrência, sem alterar qualquer cadastro existente.
- **FR-007**: Todas as validações de preenchimento, formato, política e igualdade de senhas, duplicidade de e-mail e comprovação da senha atual DEVEM ocorrer ao clicar em “Atualizar cadastro”, posicionado no final da tela. Digitar, sair de campos ou acionar “Alterar senha” NÃO DEVE apresentar novos resultados de validação ou consultar a validade da senha atual. Instruções estáticas, máscaras e limites de entrada permanecem disponíveis durante o preenchimento. As mesmas regras DEVEM proteger tentativas que não utilizem o formulário.
- **FR-008**: Com “Alterar senha” acionado, os três campos de senha DEVEM ser obrigatórios. No envio, a senha atual DEVE ser comprovada contra a credencial persistida e vigente do próprio titular; senha incorreta ou ausente DEVE impedir toda a atualização. A senha atual não precisa atender à política aplicada à nova senha.
- **FR-009**: Nova senha e confirmação DEVEM cumprir FR-007 e FR-018 da feature 004: 12 a 128 pontos de código Unicode, ao menos uma maiúscula, uma minúscula, um dígito de 0 a 9 e um símbolo que não seja letra, dígito ou espaço; ambas devem ser exatamente iguais, sem normalizar senha. Espaços contam no comprimento, mas não como símbolo. Critérios DEVEM ser informados antes do envio, com indicação de violações somente após enviar.
- **FR-010**: A atualização DEVE persistir somente campos cujo valor final difira do valor carregado, considerando as normalizações de cadastro; informação não editada DEVE permanecer como está, inclusive diante de alterações de outros campos por outra sessão. No envio, se o valor vigente de qualquer campo pessoal editado diferir do carregado na abertura, após as mesmas normalizações, o sistema DEVE rejeitar a atualização inteira, inclusive eventual troca de senha, sem sobrescrever a alteração anterior. DEVE orientar o usuário a revisar os dados atuais, preservando o rascunho não secreto para correção. Alterações em campos não editados NÃO DEVEM, por si só, impedir o envio. Envio sem mudanças efetivas DEVE informar que não há alterações, sem regravar dados ou credenciais.
- **FR-011**: Dados pessoais efetivamente alterados e eventual nova senha DEVEM ser salvos como uma única operação. Qualquer erro de validação, comprovação, duplicidade ou gravação DEVE preservar integralmente os dados cadastrais e as credenciais anteriores, sem atualização parcial nem evento de atualização cadastral. Metadados de proteção contra tentativas e de atividade da sessão PODEM ser atualizados conforme as regras existentes; essa exceção NÃO permite alterar perfil ou senha.
- **FR-012**: O sistema DEVE apresentar confirmação em português somente após gravação concluída. Erros corrigíveis DEVEM ser associados aos campos em português, sem expor detalhes internos e preservando os dados não secretos preenchidos para correção. Após sucesso ou abandono da tela, todos os campos de senha DEVEM ser descartados.
- **FR-013**: A atualização DEVE preservar o identificador do titular, seus vínculos, contas, movimentações, obrigações, pagamentos e demais dados financeiros. Alteração de e-mail DEVE mudar o identificador usado nos próximos logins; alteração de senha DEVE substituir a credencial aceita nos próximos logins, sem criar outro titular.
- **FR-014**: Consulta e atualização DEVEM exigir sessão válida e limitar o acesso ao próprio titular, recusando tentativas de selecionar outro usuário. Sessão ausente, expirada ou revogada DEVE exigir autenticação sem divulgar ou alterar cadastro. DEVEM ser preservadas as proteções existentes de sessão, contra alterações forjadas e contra tentativas repetidas de comprovação de credenciais. Senhas NÃO DEVEM ser armazenadas de forma recuperável nem expostas em respostas ou logs; dados pessoais não DEVEM aparecer em logs.

### Key Entities *(include if feature involves data)*

- **Titular / usuário**: Identidade existente com nome completo, apelido opcional, nascimento, e-mail e telefone; mantém a titularidade do histórico financeiro durante a atualização.
- **Credencial de acesso**: E-mail de login e comprovação protegida da senha vigente; pode ser substituída mediante as validações exigidas.
- **Sessão autenticada**: Permissão vigente para consultar e atualizar exclusivamente o cadastro do titular identificado.
- **Alterações cadastrais**: Conjunto de diferenças efetivas entre os dados carregados e os valores finais enviados, distinguindo preservação de valor e remoção explícita do apelido. Os valores carregados dos campos pessoais editados são a referência para detectar conflito com alterações posteriores, sem usar campos não editados como motivo de rejeição.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em 100% dos cenários com login válido, com e sem contas financeiras, o usuário chega ao formulário preenchido por uma única seleção de “Atualizar Cadastro” na navegação principal. (US1; FR-001–003)
- **SC-002**: Em 100% dos cenários de atualização de um único campo e de remoção do apelido, somente a alteração solicitada é observada ao reabrir a tela; demais dados pessoais, credencial não alterada e histórico financeiro permanecem iguais. Envio sem mudanças preserva integralmente o estado anterior. (US1; FR-004–006, FR-010, FR-013)
- **SC-003**: Em 100% dos cenários de campos inválidos, senha atual incorreta, nova senha inválida, confirmação divergente, duplicidade e falha de gravação, os dados cadastrais e as credenciais permanecem inalterados, nenhum evento de atualização cadastral é gerado e o usuário recebe orientação em português somente após o envio; nenhuma validação é antecipada por digitação ou saída de campo. Metadados de proteção contra tentativas e de atividade da sessão podem ser atualizados conforme as regras existentes, inclusive o incremento de falhas de comprovação, sem alterar perfil ou senha. (US1–US3; FR-005–012)
- **SC-004**: Em 100% dos cenários de troca de senha bem-sucedida, novo login aceita a nova senha e rejeita a anterior quando distintas; mudança de e-mail permite acessar a mesma identidade pelo novo endereço e impede login pelo anterior. (US1–US2; FR-008–009, FR-013)
- **SC-005**: Em 100% das tentativas verificadas sem sessão válida ou envolvendo outro titular, nenhum cadastro é divulgado ou alterado; tentativas concorrentes mantêm um único titular por e-mail normalizado. Em 100% dos cenários em que um campo pessoal editado mudou desde a abertura, todo o segundo envio é rejeitado sem alterar perfil ou senha; edições válidas em campos diferentes preservam ambas as alterações. (US3; FR-006, FR-010–011, FR-014)
- **SC-006**: Em avaliação com cinco participantes usando dados sintéticos, pelo menos quatro conseguem localizar a opção e concluir uma atualização válida, incluindo a troca opcional de senha, em até cinco minutos, sem assistência. (US1–US2)

## Assumptions

- Esta é uma nova feature, dependente do cadastro e login da [feature 004](../004-user-registration/spec.md), e passa a cobrir edição posterior do perfil e troca de senha, antes fora do escopo daquela feature. Nenhuma regra do cadastro inicial é modificada.
- “Mesmos campos de register” significa os mesmos dados pessoais e, mediante “Alterar senha”, nova senha e confirmação, acrescidos da senha atual; senha existente nunca é recuperada ou pré-preenchida.
- A exigência de validar somente no envio prevalece nesta tela sobre indicadores dinâmicos de validade eventualmente presentes em register, sem alterar as regras de aceitação dos valores.
- Após sucesso, a tela apresenta os dados atualizados e a confirmação. Não se introduz nova política de duração ou revogação de sessões; permanecem as regras de segurança existentes, a serem consideradas no planejamento da troca de credenciais.
- Mudanças apenas de representação que resultem no mesmo valor após normalização não exigem regravação. Limpar campo obrigatório é inválido; limpar apelido é uma alteração válida.
- Recuperação de senha esquecida, administração de outros usuários, exclusão de cadastro, confirmação de e-mail/telefone e novas regras financeiras ficam fora do escopo.
- Esta etapa entrega somente especificação e checklist. Planejamento, contratos, tarefas e implementação dependem das respectivas fases e revisões.
