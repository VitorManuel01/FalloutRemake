# Devolução conjunta na bolsa

**Devolver tudo à origem** atua sobre o recipiente de origem do item selecionado,
incluindo outras pilhas da mesma origem. A dica informa a quantidade e esclarece
que outras origens permanecem na bolsa. A ação se mantém bloqueada em outra
área, ao longe, durante movimento ou sem itens carregados.

A solicitação é vinculada à janela, token da sessão e item atualmente selecionado.
No clique, o domínio verifica novamente posição e elegibilidade antes da troca
atômica. O diálogo atualiza bolsa, peso, seleção e estado visual do recipiente.

`RETURN_ALL_UI_OK` usa as duas pilhas originais do baú superior: devolve ambas
em uma revisão, recusa repetição e callbacks antigos com uma unidade atualmente
carregada, inclusive depois de carregar checkpoint. Ao recolher novamente,
compara o checkpoint completo com o estado inicial do teste: personagem, XP,
portas, origem, explosivo consumido e cenário devem coincidir byte a byte.
A integração da bolsa em outra área também exige o botão bloqueado.

Build sem avisos/erros e integração headless específica aprovados:
`.local/return-all-ui-build.log`, `.local/return-all-ui.log` e stderr vazio.
O marcador passa a ser obrigatório na bateria e nos modos gráficos aplicáveis.
Os testes de domínio desta base somam 239 grupos.

Próximo incremento: consolidar a evidência da porta final e validar novamente
todo o percurso, incluindo leitura, coleta/devolução e checkpoints.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
