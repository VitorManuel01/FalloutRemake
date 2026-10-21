# Projeção 3D isolada da saída

**Ver em 3D** alterna o traçado por uma vista do cenário original com volumes
provisórios. O NPC do ensaio tem cor azul-clara e rótulo próprio; outras criaturas
mantêm outra cor. A câmera acompanha o NPC. **Ver traçado** retorna às três rotas.

O painel é um `SubViewport` com mundo 3D próprio, sem entrada de mouse/teclado
ou picking. Seus nós são construídos dos registros importados, preservando chave
e ID; não duplica os nós do cenário ativo. Malha, câmera e porta refletem o mesmo
snapshot que dirige o grafo lógico do ensaio. A ocultação final também esconde o
rótulo. Arte final e colisão física não fazem parte deste painel.

A integração confere separação dos mundos, posição/visibilidade do NPC, identidade
fonte, porta e restauração da espera parcial. As transformações do NPC/porta da
partida e a referência do grafo ativo permanecem intactas. Fecha/reabre sem
vazamentos nos logs do Godot. Marcador: `TEMPLE_NPC_DEPARTURE_3D_OK`.

A bateria de 18 etapas passou com esta integração:
`.local/temple-validation-20261021-023342-a11e8cdd/manifest.json`.
As capturas em desktop isolado 1280×720 e 1024×768 passaram. O refinamento final
amplia a vista, destaca o NPC e troca a legenda conforme o modo. Resultado final:
`.local/npc-departure-3d-final-isolated.json`, `Passed=true`, logs limpos e
desktop de entrada `Default` antes/depois.

Referência da API: [Viewport com mundo próprio](https://docs.godotengine.org/en/stable/classes/class_viewport.html#class-viewport-property-own-world-3d)
e [SubViewport](https://docs.godotengine.org/en/stable/classes/class_subviewport.html).

Próximo: incorporar pose/evento e variáveis Speech ao checkpoint do mundo em uma
transação única, com projeção do bloqueador em todas as consultas de interação.
O ensaio permanece separado até essa integração ter validação e rollback completos.
