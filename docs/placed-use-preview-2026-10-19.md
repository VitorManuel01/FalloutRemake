# Marco 55 — visualizar alocações posicionadas

Alocações `Placed` carregadas no checkpoint aparecem numa camada própria, com
forma provisória âmbar e indicação do tempo lógico restante. A projeção usa o
hex original no mesmo referencial da elevação inspecionada. O rótulo arredonda
frações de segundo para cima; em zero informa que aguarda resolução.

Cada nó guarda GUID, chaves de origem, mapa fonte, PID ativo e ticks. Não recebe
ID de objeto MAP, não entra na seleção/contagem dos objetos importados e não
possui colisão. Apenas o mapa e a elevação do posicionamento mostram a alocação.
O filtro Cenário também controla essa camada. Reset remove os nós; carregar
recria a mesma identidade; a conclusão lógica remove a forma consumida.

O teste `PLACED_USE_PREVIEW_OK` usa uma sessão isolada: marca como carregada a
unidade original 364/PID 85 do pote 358, sem simular uma coleta jogável, e usa a
operação pública de posicionamento. Confere geometria, identidades, filtros,
600 → 425 ticks, save/reset/load, troca de elevação, vencimento e remoção.
Contagens, navegação, ator e reconstrução dos bytes originais são preservados.

Não há botão de uso, relógio em tempo real, explosão ou destruição de cenário.
A figura é uma representação de teste e não arte original do explosivo.

No marco 58, o ensaio inicial acima foi substituído por caminhada e coleta real
na elevação 1, preservando as mesmas verificações visuais e de checkpoint.

Validação: build sem avisos/erros e integração headless aprovada. O marcador foi
acrescentado à bateria unificada. Captura gráfica somente pelo runner isolado.

Próximo: proposta de efeito que identifique a barreira suportada dentro do raio,
sem mutações, antes de persistir substituição de cenário. GPT-6.1 Sol, esforço Alto.
