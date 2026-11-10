# 001 · Fundação financeira

## Objetivo

Acessar a aplicação local como titular e cadastrar contas financeiras que servirão de origem ou destino das movimentações.

## Cenários de uso e testes

### US1 · Acessar a aplicação como titular (P1)

**Teste independente.** Entrar, sair e confirmar que a API recusa uma sessão ausente.

**Critérios.** FND-01

### US2 · Gerenciar contas financeiras (P2)

**Teste independente.** Cadastrar, editar, arquivar e reativar uma conta pela interface.

**Critérios.** FND-02–04

## Requisitos

### Critérios de aceitação

- **FND-01 · Acesso.** Um titular provisionado entra e sai da aplicação. Uma sessão ausente ou expirada impede acesso à API financeira. Identificadores enviados pelo cliente jamais concedem acesso a recursos de outro titular.
- **FND-02 · Contas.** O titular cria uma conta com nome e tipo (corrente, poupança ou carteira), consulta e altera nome e tipo. Registros permanecem disponíveis após reiniciar a aplicação. Contas novas começam em BRL, sem saldo inicial cadastrado.
- **FND-03 · Arquivamento.** A conta arquivada segue visível no histórico e fica indisponível para novas movimentações. O titular pode reativá-la.
- **FND-04 · Validação.** Nome em branco e tipo desconhecido são rejeitados com mensagem compreensível. As telas indicam sessão expirada, lista vazia e falhas de gravação.

## Escopo

Sessão de um titular e cadastro de contas. O saldo inicial poderá ser representado por uma movimentação na feature 002. Cartões, contas compartilhadas e múltiplas moedas ficam para evolução.

### Entidades principais

Titular, sessão e conta financeira.

## Critérios de sucesso

O titular consegue retomar o cadastro após reiniciar a aplicação.
