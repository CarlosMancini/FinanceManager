# 002 · Movimentações manuais

## Objetivo

Registrar receitas e despesas realizadas e consultar o histórico por conta e período.

## Clarifications

### Session 2026-11-09

- Q: Como o histórico deve exibir as movimentações canceladas? → A: Exibir movimentações canceladas na lista padrão, identificadas como **Cancelada**.
- Q: Se o envio de uma movimentação for interrompido e o formulário repetir a mesma tentativa, quantos lançamentos devem ser registrados? → A: Registrar uma única movimentação por tentativa. Uma nova tentativa intencional pode registrar dados iguais.
- Q: Qual período o histórico deve mostrar ao abrir a tela, antes de o titular escolher as datas? → A: Mostrar o mês civil atual, no fuso America/Sao_Paulo.

## Cenários de uso e testes

### US1 · Registrar e corrigir movimentações (P1)

**Teste independente.** Registrar uma despesa, corrigir seu valor e cancelar o lançamento.

**Critérios.** MOV-01, MOV-03

### US2 · Consultar histórico e totais (P2)

**Teste independente.** Filtrar um período paginado e conferir os totais com os lançamentos ativos.

**Critérios.** MOV-02, MOV-04

## Requisitos

### Critérios de aceitação

- **MOV-01 · Registro.** O titular registra receita ou despesa com conta ativa, data civil até hoje, valor positivo em BRL e descrição. Valores inválidos, datas futuras e contas arquivadas são rejeitados. A data informada é preservada. Repetir o envio da mesma tentativa de registro deve resultar em uma única movimentação, inclusive quando o primeiro envio foi salvo antes de uma interrupção. Uma nova tentativa pode registrar outra movimentação com os mesmos dados.
- **MOV-02 · Histórico.** O titular consulta movimentações por conta, período inclusivo e natureza. Ao abrir o histórico, a lista e os totais usam o período do primeiro ao último dia do mês civil atual em America/Sao_Paulo, com ambos os dias incluídos. A lista apresenta registros do mais recente ao mais antigo, inclusive para contas arquivadas. A lista padrão inclui movimentações canceladas e as identifica com o status **Cancelada**.
- **MOV-03 · Correção.** O titular altera ou cancela um lançamento manual sem baixa vinculada. O cancelamento preserva o histórico e retira o lançamento dos totais. Uma movimentação cancelada não pode ser editada ou conciliada. Quando houver vínculo com uma baixa, o titular deve desfazer a baixa antes de alterar ou cancelar a movimentação.
- **MOV-04 · Totais.** Para o mesmo filtro, os totais refletem todas as movimentações ativas, inclusive quando a lista é paginada. O líquido é receitas menos despesas. O valor representa movimentação do período.

Transferências, lançamentos futuros, categorização e importação ficam para evolução.

### Entidades principais

Conta financeira e movimentação manual.

## Critérios de sucesso

O líquido do período coincide com receitas menos despesas em todo o filtro, inclusive com paginação.
