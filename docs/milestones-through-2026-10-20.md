# Resumo de todos os marcos até 20/10/2026

Retomada posterior: [doze incrementos adicionais e validações](continuation-batch-2026-10-20.md).
Retomada anterior: [onze incrementos de estado, porta e consulta do NPC](temple-state-batch-2026-10-20.md).
Retomada mais recente: [quatro marcos da saída restaurável do NPC e vista 3D](temple-npc-batch-2026-10-20.md).

O texto abaixo preserva a parada histórica. O estado mais recente e as
validações atuais estão na última retomada acima.

Parada solicitada pelo usuário ao concluir a análise adicional do baú.
Histórico conferido no Git e nos registros do projeto. A tabela agrupa os marcos
numerados de 1 a 59; a lista seguinte cobre todos os incrementos posteriores.
Documentos de planejamento e commits de manutenção não significam novas funções.

| Marcos | Entrega |
| --- | --- |
| 1 | Projeto e Godot .NET compilando; dados Epic e referência Community Engine localizados; leitura DAT e inspeção inicial. |
| 2 | MAP/PRO/scripts/inventários interpretados; reconstrução byte a byte; IDs e origem preservados. Correção: interior ARCAVES, exterior ARTEMPLE. |
| 3 | Prévia isométrica 3D com formas provisórias, inspeção e filtros por elevação. |
| 4 | Navegação por hexágonos e movimento por caminhos válidos. |
| 5 | Primeira porta: aproximação, abrir/fechar e atualização da navegação. |
| 6 | Leitor INT e condições originais da segunda porta analisadas sem executar scripts. |
| 7–8 | Adaptador de arrombamento e controles na prévia; perfil mínimo, resultados controlados e 25 XP sem repetição. |
| 9–10 | Passagens entre elevações e áreas externas; posição, portas e áreas visitadas preservadas. |
| 11 | Checkpoint local validado, com restauração do estado do mundo. |
| 12–15 | Câmera no personagem, piso original opcional, parar caminhada sem reiniciar e ocultação de paredes que encobrem o personagem. |
| 16–18 | Nomes/descrições originais, consulta do inventário recursivo e grade de navegação opcional. |
| 19–21 | Percurso antes do clique, aproximação à porta e informação sobre destino das saídas. |
| 22–24 | Validação gráfica em desktop isolado, enquadramento do percurso e preferências visuais persistentes. |
| 25–26 | Transferência conservadora de itens, vínculo com a origem e checkpoint v2 compatível com v1. |
| 27–29 | Recolher, consultar bolsa e devolver itens ao recipiente original, respeitando proximidade e quantidades. |
| 30–31 | Clique e prévia de aproximação aos recipientes; retorno à origem pela bolsa. |
| 32–35 | Pesos originais PRO, capacidade de carga do perfil Força 5, carga nas janelas e escolha de quantidades. |
| 36–37 | Baú compartilhado por scripts analisado e recipientes vazios especiais identificados, sem simular interação não suportada. |
| 38–39 | Planejamento de volta por áreas visitadas e orientação explícita pelas passagens. |
| 40–41 | Leitura FRM/paleta dos ícones originais e apresentação na coleta/bolsa com fallback. |
| 42 | Coleta e caminhos entre elevações auditados; barreira original preservada. |
| 43–45 | Condição da barreira, substituição, distância em hexes, alcance e temporizador do explosivo conferidos nos dados e no CE. |
| 46–48 | Ícones na consulta original, proteção da caminhada ao inspecionar elevações e bateria reproduzível com manifesto. |
| 49–52 | Proposta de uso, conservação entre carregado/posicionado/consumido, checkpoint v3 e aplicação com revalidação de sessão e revisão. |
| 53–55 | Tempo lógico de usos pendentes, parada no primeiro vencimento e representação separada do item posicionado. |
| 56–57 | Proposta validada do efeito na barreira e projeção separada do cenário/navegação. |
| 58–59 | Ensaio integrado com explosivo realmente coletado; continuação projetada até os recipientes da elevação 2. |

Incrementos posteriores, em ordem de integração:

1. **Entidades distinguíveis:** barreiras com X vermelho, luzes douradas,
   recipientes verdes/cinzas/roxos e rótulos conforme o conteúdo.
2. **Checkpoint v4 e efeito atômico:** consumo do explosivo e substituição da
   barreira na mesma operação, com compatibilidade dos checkpoints anteriores.
3. **Percurso aplicado:** abertura real na sessão, passagem à elevação 2,
   coleta e retorno conferidos sobre os dados originais.
4. **Integração visual da passagem:** objeto substituído com identidade própria,
   navegação atualizada e estado recuperado ao carregar.
5. **Revisão de sobreposições e interface:** recipientes visíveis no mesmo hex de
   bloqueadores, seleção consistente, painéis roláveis e janelas responsivas.
6. **Preparação pela bolsa:** escolher prazo, cancelar ou posicionar uma unidade
   do explosivo original em um ensaio com resultado de preparação controlado.
7. **Relógio na interface:** avançar +1/+10 s, parar no vencimento e preservar
   eventos pendentes ao salvar/carregar.
8. **Gravação mais resistente:** repetição limitada diante de recusa transitória,
   mantendo proteção de arquivos bloqueados e substituição atômica.
9. **Revisão do efeito:** consultar, cancelar ou confirmar a abertura da barreira
   pela interface, com revalidação e persistência.
10. **Orientação até a barreira:** caminho alcançável solicitado pela bolsa;
    preparação permitida no alcance verificado de até três hexágonos.
11. **Validação estrita do Godot:** erros nos logs e ausência de marcadores
    reprovam a execução, mesmo quando o processo termina com código zero.
12. **Localizador de recipientes:** os sete recipientes localizados pelo conteúdo
    restante, sem deslocar o personagem nem recolher itens.
13. **Guia completo do ensaio:** percurso e controles no README, documentação e
    evidências finais reunidas no pacote da bateria de 20/10.
14. **Último marco — fluxo do baú:** relatório verifica transferência jogador →
    baú, quatro referências de diálogo e a porta distinta fechada/trancada.
    Recuperação automática não comprovada; nenhuma ação fictícia foi habilitada.

Estado utilizável: explorar o templo, manipular portas, viajar, recolher/devolver
itens, preparar o explosivo, avançar o relógio, revisar a abertura e continuar
pela passagem. O checkpoint conserva esse ensaio. Arte final, combate geral,
dano, execução geral de scripts e o desafio de Cameron continuam fora do escopo
implementado. Fallout 1 permanece para depois.

Verificação da última bateria: **227 grupos sintéticos**, **7 grupos de validação
de logs**, **14/14 etapas**, builds sem avisos/erros e integração Godot headless.
Manifesto: `.local/temple-validation-20261020-154340-fb1b6eec/manifest.json`.
As capturas da bateria anterior usaram apenas desktop isolado, sem trocar o
desktop de entrada. Neste último marco não foi aberta janela gráfica.

Dados proprietários continuam apenas na instalação e em arquivos locais
ignorados. Alterações preexistentes de formatação e `.uid` foram preservadas.
Sem push. A implementação nova deste marco está em
`codex/temple-chest-return-analysis`.

Referências: [guia de execução](../README.md),
[bateria anterior](batch-2026-10-20.md),
[análise final do baú](temple-chest-flow-2026-10-20.md).

Próximo passo sugerido, sem execução após esta parada: comprovar recuperação
manual e estados do desafio do baú antes de ampliar a propriedade dos itens.
Modelo recomendado: **GPT-6.1 Sol, esforço Alto**.
