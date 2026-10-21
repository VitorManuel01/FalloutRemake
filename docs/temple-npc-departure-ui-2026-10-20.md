# Saída do NPC pela consulta Speech

Após concluir ou carregar o resultado Speech, **Saída do NPC · ensaio** abre uma
janela com as três rotas e seus hexes de parada. **Um passo** avança um hex;
**Até a próxima parada** encerra o trecho sem pular a espera. Junto à porta,
**Avançar 1 s** ou **Aguardar o evento** consomem somente os ticks pendentes.

**Salvar saída / Carregar saída** usam `.local/checkpoints/temple-departure.json`,
separado do checkpoint da partida e do resultado Speech. O carregamento restaura
inclusive uma espera parcial. **Recomeçar saída** descarta apenas o ensaio atual;
**Voltar à conversa** conserva o resultado Speech aceito.

A janela suspende os controles do cenário. Ações antigas são recusadas após
passos, carregamento ou reabertura. Os controles da conversa oculta também ficam
inativos enquanto a saída está aberta. O grafo e a posição do jogador, portas,
itens, XP e resultado Speech permanecem idênticos.

Validação: bateria completa **18/18** e **285 grupos sintéticos**, manifesto
`.local/temple-validation-20261021-022632-38ebdb73/manifest.json`. A integração
emite `TEMPLE_NPC_DEPARTURE_UI_OK`. Builds sem avisos/erros. As capturas isoladas
1280×720 e 1024×768 passaram com logs limpos e desktop de entrada `Default`
preservado; a segunda inclui os rótulos e a proteção da conversa oculta finais.

Os dados continuam sendo um ensaio manual separado. Não há aceitação ativa,
execução geral de scripts, duelo ou XP novo.
