# Marco 53 — ticks lógicos de usos pendentes

`AdvanceUseTime` reduz os ticks restantes dos posicionados, inclusive quando a
origem está em área inativa. Valores negativos são recusados; intervalos grandes
saturam em zero. Consumo, bolsa, origem, portas, posição e XP não mudam por vencer
um prazo. `DueItemUses` retém os vencidos até sua conclusão explícita interna.
Essa conclusão recusa eventos ainda antecipados ou já consumidos.

Um uso salvo após 175 de seus 600 ticks carrega com 425 restantes. Consumo
confirmado não reaparece como evento pendente. Alterar ticks incrementa a revisão
de origem, invalidando propostas anteriores; avançar zero não altera a revisão.

O relógio ainda exige chamadas explícitas. Não usa tempo de parede nem está
ligado ao `_Process`, a uma ação de uso ou à saída de mapas. O CE resolve explosivos
na saída de área por seu próprio ciclo; isso ainda não foi adaptado. O módulo
não aplica explosões, dano ou destruição de cenário, nem concede XP.

Validação: build sem avisos/erros e 195 grupos sintéticos. Cobertos prazo exato,
sucesso/falha/crítica, conclusão antecipada/duplicada, intervalos extremos,
snapshots independentes, save/load parcial, origem em área inativa e conservação.
O relatório real de checkpoint também confirma 175 ticks, carga/restauração
dos 425 restantes e conclusão lógica, sem gerar dano ou alterar os MAPs.

Próximo: tratar o avanço cronológico por próximo vencimento e o orçamento que
sobra, antes de conectar efeitos. A lista atual é uma consulta de vencidos;
não constitui ainda um dispatcher cronológico de explosões.
GPT-6.1 Sol, esforço Alto.
