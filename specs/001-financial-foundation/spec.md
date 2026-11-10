# 001 · Fundação financeira

## Objetivo

Acessar a aplicação local como titular e cadastrar contas financeiras que servirão de origem ou destino das movimentações.

## Clarifications

### Session 2026-11-09

- Q: O titular pode ter duas contas financeiras com o mesmo nome? → A: O nome é único por titular, inclusive entre contas arquivadas. A comparação ignora maiúsculas, minúsculas e espaços nas extremidades.
- Q: Quando a sessão do titular deve expirar automaticamente? → A: Após 30 minutos sem atividade autenticada.
- Q: Se o titular editar a mesma conta em duas abas, o que deve acontecer ao salvar a segunda edição com dados desatualizados? → A: A edição é rejeitada e o titular deve recarregar a conta antes de tentar novamente.

## Cenários de uso e testes

### US1 · Acessar a aplicação como titular (P1)

**Teste independente.** Entrar, sair e confirmar que a API recusa uma sessão ausente.

**Critérios.** FND-01

### US2 · Gerenciar contas financeiras (P2)

**Teste independente.** Cadastrar, editar, arquivar e reativar uma conta pela interface.

**Critérios.** FND-02–04

## Requisitos

### Critérios de aceitação

- **FND-01 · Acesso.** Um titular provisionado entra e sai da aplicação. Uma sessão ausente ou expirada impede acesso à API financeira. A sessão expira ao completar 30 minutos sem atividade autenticada. A contagem começa no login e reinicia a cada requisição autenticada aceita durante a sessão. Após a expiração, o titular precisa entrar novamente. Identificadores enviados pelo cliente jamais concedem acesso a recursos de outro titular.
- **FND-02 · Contas.** O titular cria uma conta com nome e tipo (corrente, poupança ou carteira), consulta e altera nome e tipo. Registros permanecem disponíveis após reiniciar a aplicação. Contas novas começam em BRL, sem saldo inicial cadastrado. Ao cadastrar ou renomear uma conta, o sistema rejeita o nome se outra conta do mesmo titular já o usa, inclusive se ela estiver arquivada. A comparação ignora maiúsculas, minúsculas e espaços nas extremidades. Se a conta foi alterada desde que o titular carregou os dados para edição, o sistema rejeita o salvamento, preserva os dados atuais e orienta o titular a recarregar a conta antes de tentar novamente.
- **FND-03 · Arquivamento.** A conta arquivada segue visível no histórico e fica indisponível para novas movimentações. O titular pode reativá-la.
- **FND-04 · Validação.** Nome em branco e tipo desconhecido são rejeitados com mensagem compreensível. As telas indicam sessão expirada, lista vazia e falhas de gravação.

## Escopo

Sessão de um titular e cadastro de contas. O saldo inicial poderá ser representado por uma movimentação na feature 002. Cartões, contas compartilhadas e múltiplas moedas ficam para evolução.

### Entidades principais

Titular, sessão e conta financeira.

## Critérios de sucesso

O titular consegue retomar o cadastro após reiniciar a aplicação.
