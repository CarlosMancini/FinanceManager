# 003 · Contas a pagar e baixas

## Objetivo

Registrar despesas previstas, acompanhar vencimentos e dar baixa conciliando com o realizado.

## Cenários de uso e testes

### US1 · Acompanhar obrigações e vencimentos (P1)

**Teste independente.** Cadastrar obrigações com datas diferentes e conferir os estados antes, no dia e depois do vencimento.

**Critérios.** PAG-01, PAG-02

### US2 · Dar baixa integral e reverter (P2)

**Teste independente.** Dar baixa em uma obrigação, repetir a mesma solicitação e reverter, conferindo os totais.

**Critérios.** PAG-03–05

## Requisitos

### Critérios de aceitação

- **PAG-01 · Cadastro.** O titular cria uma obrigação avulsa com descrição, valor positivo em BRL e vencimento. Pode corrigir ou cancelar enquanto estiver em aberto. Cancelamentos ficam no histórico e saem dos totais em aberto.
- **PAG-02 · Situação.** Uma obrigação em aberto aparece como a vencer, vence hoje ou vencida segundo o calendário de São Paulo. Uma baixa ativa determina a situação paga. O titular pode filtrar por situação e período de vencimento.
- **PAG-03 · Baixa integral.** O titular vincula uma despesa ativa do mesmo titular, com valor idêntico e sem outra baixa ativa, ou cria uma despesa na operação de baixa informando conta ativa e data de pagamento até hoje. Aceitar pagamento antecipado ou atrasado. A baixa retira a obrigação do total em aberto e a movimentação entra uma vez no realizado. Repetir uma solicitação bem-sucedida com a mesma chave e os mesmos dados devolve o resultado original.
- **PAG-04 · Desfazer.** O titular pode desfazer a baixa. Se a despesa já existia, ela permanece ativa. Se foi criada na baixa, é cancelada com registro da reversão. A obrigação volta a ter a situação calculada pelo vencimento.
- **PAG-05 · Integridade.** Uma obrigação e uma movimentação participam de no máximo uma baixa ativa cada. Bloquear alteração ou cancelamento de ambos enquanto estiverem vinculados. Requisições concorrentes não geram lançamentos duplicados nem deixam vínculos parciais.

Pagamentos parciais, recorrência, parcelamento, conciliação automática e execução de pagamentos ficam para evolução.

### Entidades principais

Obrigação, movimentação e vínculo de baixa.

## Critérios de sucesso

Cada baixa ativa retira uma obrigação do total em aberto e mantém exatamente uma saída no realizado.
