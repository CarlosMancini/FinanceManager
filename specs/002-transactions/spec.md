# 002 · Movimentações manuais

## Objetivo

Registrar receitas e despesas realizadas e consultar o histórico por conta e período.

## Cenários de uso e testes

### US1 · Registrar e corrigir movimentações (P1)

**Teste independente.** Registrar uma despesa, corrigir seu valor e cancelar o lançamento.

**Critérios.** MOV-01, MOV-03

### US2 · Consultar histórico e totais (P2)

**Teste independente.** Filtrar um período paginado e conferir os totais com os lançamentos ativos.

**Critérios.** MOV-02, MOV-04

## Requisitos

### Critérios de aceitação

- **MOV-01 · Registro.** O titular registra receita ou despesa com conta ativa, data civil até hoje, valor positivo em BRL e descrição. Valores inválidos, datas futuras e contas arquivadas são rejeitados. A data informada é preservada.
- **MOV-02 · Histórico.** O titular consulta movimentações por conta, período inclusivo e natureza. A lista apresenta registros do mais recente ao mais antigo, inclusive para contas arquivadas.
- **MOV-03 · Correção.** O titular altera ou cancela um lançamento manual sem baixa vinculada. O cancelamento preserva o histórico e retira o lançamento dos totais. Uma movimentação cancelada não pode ser editada ou conciliada. Quando houver vínculo com uma baixa, o titular deve desfazer a baixa antes de alterar ou cancelar a movimentação.
- **MOV-04 · Totais.** Para o mesmo filtro, os totais refletem todas as movimentações ativas, inclusive quando a lista é paginada. O líquido é receitas menos despesas. O valor representa movimentação do período.

Transferências, lançamentos futuros, categorização e importação ficam para evolução.

### Entidades principais

Conta financeira e movimentação manual.

## Critérios de sucesso

O líquido do período coincide com receitas menos despesas em todo o filtro, inclusive com paginação.
