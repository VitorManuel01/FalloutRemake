# Entidades de teste: formas, cores e conteúdo

As representações provisórias agora distinguem a função dos objetos sem alterar
os tipos importados, IDs, dados originais, navegação ou regras de coleta.

| Representação | Significado |
| --- | --- |
| Placa vermelha com X | Cenário que bloqueia a passagem |
| Poste com esfera dourada | Fonte que emite luz nos dados originais |
| Caixa verde, tampa fechada e indicador | Recipiente com itens restantes |
| Caixa cinza, tampa inclinada | Recipiente vazio |
| Caixa roxa | Recipiente com interação especial ou acesso restrito |

Os recipientes mostram um rótulo com a quantidade atual. Coletar esvazia a caixa;
devolver restaura o indicador e a cor; carregar um checkpoint recria o estado.
O recipiente com script continua identificado como especial mesmo vazio. As
atualizações usam a revisão do inventário e também funcionam com a janela de
coleta/bolsa aberta. Não é necessário reconstruir o mapa a cada transferência.

Há uma legenda na lateral e uma identificação direta no inspetor. A classificação
das luzes exige raio e intensidade positivos; criaturas, portas e paredes
preservam suas silhuetas. O desenho da lâmpada não adiciona luzes dinâmicas nem
altera a iluminação. A placa vermelha indica bloqueio, não destrutibilidade.
Os rótulos de recipientes ficam legíveis mesmo atrás de volumes provisórios.

O realce de seleção respeita a profundidade para não tingir a representação.
O painel lateral foi compactado para acomodar a legenda e manter as ações visíveis.
As formas de seleção acompanham as novas silhuetas, sem interferir na navegação.

Validação: build sem avisos/erros; 213 grupos sintéticos. A integração adicional
`ENTITY_READABILITY_OK` cobre fonte de luz real, barreira, coleta/devolução,
vazio/cheio/especial, checkpoint, legenda, espaço das ações e bytes originais.
Capturas somente pelo runner isolado, sem trocar o desktop de entrada.

Bateria final: 12/12 etapas aprovadas. Suíte gráfica final: saída 0, stderr
vazio, 26,9 segundos, desktop de entrada Default antes/depois. As capturas de
recipiente cheio/vazio, interação especial e barreira/luzes foram revisadas.

Próximo trabalho funcional continua sendo persistir e aplicar a substituição da
barreira. Modelo recomendado para esse incremento: GPT-6.1 Sol, esforço Alto.
