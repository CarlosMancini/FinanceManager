# 002 · Movimentações manuais

## Objetivo

Registrar receitas e despesas realizadas e consultar o histórico por conta e período.

## Clarifications

### Session 2026-11-09

- Q: Como o histórico deve exibir as movimentações canceladas? → A: Exibir movimentações canceladas na lista padrão, identificadas como **Cancelada**.
- Q: Se o envio de uma movimentação for interrompido e o formulário repetir a mesma tentativa, quantos lançamentos devem ser registrados? → A: Registrar uma única movimentação por tentativa. Uma nova tentativa intencional pode registrar dados iguais.
- Q: Qual período o histórico deve mostrar ao abrir a tela, antes de o titular escolher as datas? → A: Mostrar o mês civil atual, no fuso America/Sao_Paulo.

### Session 2026-11-10

- Q: Qual tela deve abrir após o login? → A: Abrir **Movimentações**, exceto quando o titular ainda não cadastrou nenhuma conta; nesse caso, abrir diretamente o cadastro de conta.
- Q: Como impedir cadastro de movimentação sem uma conta utilizável? → A: Desabilitar **Nova movimentação** quando não houver conta ativa e redirecionar acessos diretos ao formulário para o cadastro de conta quando não houver nenhuma conta cadastrada.
- Q: Como deve funcionar o registro consecutivo? → A: Após cada cadastro concluído, perguntar **“Adicionar mais uma movimentação?”**. **Sim** limpa o formulário e inicia uma nova tentativa; **Não** retorna ao histórico.
- Q: Como reduzir alterações acidentais? → A: Exigir confirmação explícita antes de salvar uma correção e antes de cancelar uma movimentação.

## Cenários de uso e testes

### US1 · Registrar e corrigir movimentações (P1)

**Teste independente.** Registrar uma despesa com todos os campos preenchidos e valor positivo, corrigir seu valor e cancelar o lançamento. Tentar registrar com cada campo vazio, valor zero e valor negativo.

**Critérios.** MOV-01, MOV-03

### US2 · Consultar histórico e totais (P2)

**Teste independente.** Filtrar um período paginado e conferir os totais com os lançamentos ativos.

**Critérios.** MOV-02, MOV-04

### US3 · Navegar e registrar movimentações com segurança (P1)

**Teste independente.** Entrar sem contas, cadastrar a primeira conta, registrar duas movimentações consecutivas com valores formatados em BRL e confirmar uma correção e um cancelamento.

**Critérios.** MOV-05, MOV-06, MOV-07

## Requisitos

### Critérios de aceitação

- **MOV-01 · Registro.** O titular registra receita ou despesa com conta ativa, data civil até hoje, valor estritamente maior que zero em BRL e descrição. Na tela **Nova movimentação**, conta, natureza, data, valor e descrição são obrigatórios; nenhum valor inicial pode dispensar a escolha ou confirmação dos dados exigidos para enviar. Campos ausentes, valor zero, valor negativo, valores inválidos, datas futuras e contas arquivadas são rejeitados, inclusive em solicitações fora da interface. A data informada é preservada. Repetir o envio da mesma tentativa de registro deve resultar em uma única movimentação, inclusive quando o primeiro envio foi salvo antes de uma interrupção. Uma nova tentativa pode registrar outra movimentação com os mesmos dados.
- **MOV-02 · Histórico.** O titular consulta movimentações por conta, período inclusivo e natureza. Ao abrir o histórico, a lista e os totais usam o período do primeiro ao último dia do mês civil atual em America/Sao_Paulo, com ambos os dias incluídos. A lista apresenta registros do mais recente ao mais antigo, inclusive para contas arquivadas. A lista padrão inclui movimentações canceladas e as identifica com o status **Cancelada**.
- **MOV-03 · Correção.** O titular altera ou cancela um lançamento manual sem baixa vinculada. O cancelamento preserva o histórico e retira o lançamento dos totais. Uma movimentação cancelada não pode ser editada ou conciliada. Quando houver vínculo com uma baixa, o titular deve desfazer a baixa antes de alterar ou cancelar a movimentação.
- **MOV-04 · Totais.** Para o mesmo filtro, os totais refletem todas as movimentações ativas, inclusive quando a lista é paginada. O líquido é receitas menos despesas. O valor representa movimentação do período.
- **MOV-05 · Entrada e disponibilidade.** Após login bem-sucedido, o titular entra no histórico de movimentações. Quando ainda não possui nenhuma conta cadastrada, entra diretamente na tela de cadastro de conta, com o formulário disponível para preenchimento. No histórico, **Nova movimentação** fica desabilitada quando não existe conta ativa. Se o titular acessar diretamente o formulário de nova movimentação sem possuir nenhuma conta cadastrada, é redirecionado para o cadastro de conta. Contas arquivadas continuam disponíveis apenas para consulta e não habilitam novos lançamentos.
- **MOV-06 · Entrada monetária e registro consecutivo.** O campo **Valor em BRL** aceita apenas dígitos digitados ou colados, exibe automaticamente a máscara `R$ X,XX` enquanto o titular preenche e preserva centavos; letras, sinais, separadores digitados e valor não positivo não podem resultar em lançamento. O valor exibido corresponde ao valor registrado. Após cada registro concluído, a interface apresenta o diálogo **“Adicionar mais uma movimentação?”**, com opções **Sim** e **Não**. **Sim** permanece no formulário, limpa os campos editáveis, aplica os valores iniciais de um novo lançamento e gera uma nova tentativa independente. **Não** retorna ao histórico de movimentações. Fechar o diálogo equivale a **Não**.
- **MOV-07 · Confirmações de alteração.** Antes de enviar uma correção, a interface apresenta um diálogo que identifica a ação e oferece **Confirmar** e **Voltar**. Antes de cancelar uma movimentação, apresenta confirmação equivalente deixando claro que o cancelamento é definitivo. **Voltar** ou fechar o diálogo mantém a movimentação e os dados do formulário sem alterações. Uma confirmação aceita usa a versão atualmente carregada e mantém o tratamento de concorrência definido em MOV-03.

Transferências, lançamentos futuros, categorização e importação ficam para evolução.

### Entidades principais

Conta financeira e movimentação manual.

## Critérios de sucesso

- **SC-01.** Em 100% dos filtros verificados, o líquido do período coincide com receitas menos despesas no conjunto completo, inclusive quando há mais de uma página.
- **SC-02.** Em 100% dos acessos após login sem contas cadastradas, o primeiro formulário financeiro exibido é o cadastro de conta, sem ação intermediária do titular.
- **SC-03.** Em 100% dos caminhos da interface verificados sem conta ativa, nenhum lançamento pode ser iniciado ou enviado.
- **SC-04.** O titular registra duas movimentações consecutivas usando um único acesso ao formulário de nova movimentação, e 100% das correções e cancelamentos exigem confirmação explícita antes do envio.
- **SC-05.** Em 100% das tentativas verificadas de cadastro com um campo obrigatório vazio ou valor zero ou negativo, nenhuma movimentação é criada; em 100% das entradas monetárias verificadas, apenas dígitos são aceitos e o valor exibido em BRL coincide com o registrado.

## Suposições

- “Tela de cadastro de nova conta” é a tela autenticada de contas com o formulário de cadastro disponível e em foco; não exige uma nova entidade ou um fluxo de criação distinto.
- O redirecionamento obrigatório ao cadastro ocorre quando não existe nenhuma conta. Se existem apenas contas arquivadas, o histórico permanece acessível, mas **Nova movimentação** fica desabilitada porque nenhuma conta aceita lançamentos.
- Ao escolher **Sim** após um registro, o novo formulário usa a primeira conta ativa disponível, natureza **Despesa**, valor e descrição vazios e a data civil atual em America/Sao_Paulo.
