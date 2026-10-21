# Speech e saída manual na partida

Conclua as cinco escolhas verificadas em **Conversa · ensaio**. **Aplicar à
partida** aceita esse resultado com INT 5/Speech marcado e inicia a sequência
manual do NPC. Consultar, salvar o ensaio ou fechar a janela não aplica nada.

O botão **Saída do NPC** revela os controles no painel lateral. **Um passo**
avança um hex; **Próxima parada** percorre o trecho até a porta, o destino
intermediário ou a saída. Todos os hexes são revalidados. Se você ocupar o
próximo hex, afaste-se e continue. Sua caminhada precisa terminar antes de
avançar o NPC. Não há animação ou relógio automático.

Junto à porta, **+1 s / Esperar evento** avançam apenas essa espera. **K/R**
salvam/restauram toda a partida, inclusive a posição, o bloqueador, a fase e uma
espera parcial. **Enquadrar NPC** move somente a câmera. Após a ocultação final,
o NPC deixa de bloquear e de receber cliques, inclusive com o filtro de ocultos.

A cena projeta a mesma pose usada pela navegação e pelas consultas de seleção,
alcance e aproximação. Recarregar recompõe malha, posição, rotação, legenda e
estado da porta. O objeto fonte, suas chaves e IDs não são reescritos.

Validação: **18/18 etapas**, manifesto
`.local/temple-validation-20261021-031753-ea56869c/manifest.json`, **294 grupos
sintéticos** e builds sem avisos/erros. O teste Godot emite
`TEMPLE_ACTIVE_SPEECH_OK`: aplicação explícita, ocupação, caminhada em andamento,
ações antigas, K/R em movimento e na espera, arquivo contraditório recusado,
três trechos, ocultação, dois itens e 25 XP preservados. O fixture restaura
integralmente o estado anterior ao terminar.

O duelo, uma VM geral e recompensas adicionais continuam fora deste perfil.
