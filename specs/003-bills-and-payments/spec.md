# 003 · Contas a pagar e baixas

## Objetivo

Registrar despesas previstas, acompanhar vencimentos e dar baixa conciliando com o realizado.

## Clarifications

### Session 2026-11-10

- Q: Ao dar baixa em uma obrigação, o titular pode vincular uma despesa que continua ativa em uma conta arquivada? → A: Permitir o vínculo com a despesa ativa da conta arquivada.
- Q: Ao abrir a tela de contas a pagar, quais obrigações devem aparecer antes de aplicar filtros? → A: Mostrar todas as obrigações em aberto, de qualquer mês.
- Q: Qual descrição deve ser registrada na despesa criada ao dar baixa em uma obrigação? → A: Copiar automaticamente a descrição da obrigação.
- Q: Quando o titular filtra as contas a pagar, quais obrigações devem compor o total em aberto? → A: Somar as obrigações em aberto que correspondem aos filtros de situação e período de vencimento.

## Cenários de uso e testes

### US1 · Acompanhar obrigações e vencimentos (P1)

**Teste independente.** Cadastrar obrigações com datas diferentes, conferir os estados antes, no dia e depois do vencimento e verificar que a lista inicial inclui obrigações vencidas em meses anteriores. Aplicar filtros de situação e período de vencimento e conferir o total em aberto.

**Critérios.** PAG-01, PAG-02

### US2 · Dar baixa integral e reverter (P2)

**Teste independente.** Dar baixa em uma obrigação criando uma despesa, conferir o valor e a descrição no histórico, repetir a mesma solicitação e reverter, conferindo os totais.

**Critérios.** PAG-03–05

## Requisitos

### Critérios de aceitação

- **PAG-01 · Cadastro.** O titular cria uma obrigação avulsa com descrição, valor positivo em BRL e vencimento. Pode corrigir ou cancelar enquanto estiver em aberto. Cancelamentos ficam no histórico e saem dos totais em aberto.
- **PAG-02 · Situação.** Uma obrigação em aberto aparece como a vencer, vence hoje ou vencida segundo o calendário de São Paulo. Uma baixa ativa determina a situação paga. A lista inicial inclui todas as obrigações em aberto, sem restrição de período. O titular pode filtrar por situação, inclusive paga e cancelada, e por período de vencimento. O total em aberto soma todas as obrigações em aberto que correspondem aos dois filtros.
- **PAG-03 · Baixa integral.** O titular vincula uma despesa ativa do mesmo titular, com valor idêntico e sem outra baixa ativa, inclusive se a conta da despesa estiver arquivada. Se criar uma despesa na baixa, o titular informa uma conta ativa e uma data de pagamento até hoje. O sistema copia o valor e a descrição da obrigação para a nova despesa. Aceitar pagamento antecipado ou atrasado. A baixa retira a obrigação do total em aberto e a movimentação entra uma vez no realizado. Repetir uma solicitação bem-sucedida com a mesma chave e os mesmos dados devolve o resultado original.
- **PAG-04 · Desfazer.** O titular pode desfazer a baixa. Se a despesa já existia, ela permanece ativa. Se foi criada na baixa, é cancelada com registro da reversão. A obrigação volta a ter a situação calculada pelo vencimento.
- **PAG-05 · Integridade.** Uma obrigação e uma movimentação participam de no máximo uma baixa ativa cada. Bloquear alteração ou cancelamento de ambos enquanto estiverem vinculados. Requisições concorrentes não geram lançamentos duplicados nem deixam vínculos parciais.

Pagamentos parciais, recorrência, parcelamento, conciliação automática e execução de pagamentos ficam para evolução.

### Entidades principais

Obrigação, movimentação e vínculo de baixa.

## Critérios de sucesso

Para cada combinação de filtros, o total em aberto coincide com a soma das obrigações em aberto que correspondem aos filtros. Cada baixa ativa exclui a obrigação do conjunto em aberto e mantém exatamente uma saída no realizado.
