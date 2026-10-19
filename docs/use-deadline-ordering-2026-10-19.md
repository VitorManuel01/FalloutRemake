# Marco 54 — avançar até o próximo vencimento

`AdvanceUseTime` retorna `AppliedTicks`, `UnusedTicks` e `Due`. O avanço para no
primeiro vencimento; um evento já vencido bloqueia novo avanço até sua conclusão.
Isso impede que um intervalo grande processe um prazo de 600 ticks antes de um
prazo de 300, ou que o relógio passe por uma falha crítica imediata sem tratá-la.

O chamador conserva a sobra para uma chamada posterior, depois de resolver todos
os vencidos. Vencimentos simultâneos devem ser concluídos antes de continuar.
A consulta usa ordem determinística de área fonte/GUID para empates; não declara
equivalência com a ordem de registro da fila nativa CE. Nenhum efeito foi ligado.

Posicionamentos criados em instantes diferentes mantêm seus prazos relativos.
Salvar com um evento vencido preserva o bloqueio e os ticks dos seguintes.
O orçamento não aplicado pertence ao chamador e não representa tempo já avançado
no mundo. Sem eventos pendentes, o método aceita todo o orçamento sem alterar itens.

Validação: build sem avisos/erros e 199 grupos sintéticos aprovados. Novos cenários
cobrem intervalos longos, evento imediato, prazos diferentes, criação posterior,
save/load com bloqueio, empates e mundo sem eventos. Não há relógio de parede,
dano, abertura de barreira, consumo automático ou ação de uso na interface.

Próximo: visualizar a alocação posicionada e sua identidade separada dos registros
originais, antes de conectar efeitos de explosão. GPT-6.1 Sol, esforço Alto.
