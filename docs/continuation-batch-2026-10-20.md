# Continuação da bateria — 20/10/2026

Base `31a846a`. Doze incrementos concluídos; todos seguem na mesma linhagem de
branches `codex/`, sem push. O [histórico anterior](milestones-through-2026-10-20.md)
permanece disponível, incluindo os marcos 1–59 e os incrementos já entregues.

| Commit | Marco |
| --- | --- |
| `20481a2` | Caminho nativo de recuperação manual do baú, fallback dos procedimentos ausentes e cabeçalho da animação conferidos. |
| `d265a84` | Janela J explica a função do baú do desafio, mantendo ações bloqueadas e fallback genérico para outros recipientes. |
| `d4ea96f` | Retirada conjunta atômica: todas as pilhas devem ser suportadas e caber na carga total, incluindo outras áreas. |
| `c42423f` | **Retirar tudo** na janela J, com coleta real de duas pilhas e proteção contra repetição/callback antigo. |
| `6b07969` | Leitor de referências literais de diálogo; 53 opções do programa do NPC identificadas sem executar o INT. |
| `06140ef` | Alternativa por Speech: condições e quatro escritas em espaços de variáveis distintos conferidas. |
| `8679e61` | Devolução atômica de todas as pilhas da mesma origem; itens de outras origens e unidades usadas preservados. |
| `fa4099b` | **Devolver tudo à origem** na bolsa, com seleção/sessão/janela revalidadas e checkpoint real. |
| `72d65b0` | Escritas constantes e ciclo inicial/de atualização da porta final conferidos, sem habilitar seu script. |
| `16c9348` | Metadados `local_vars` de scripts.lst lidos por índice físico; alocação inicial do CE documentada. |
| `55d89f2` | Auditoria dos getters literais detecta duas referências além da contagem declarada do NPC. |
| `444e73c` | Comparação patch/master mantém prioridade e comprova a mesma contagem; limites nativos de armazenamento registrados. |

## O que mudou no uso

- Em J, **Retirar tudo** recolhe todas as pilhas restantes do recipiente, sem
  recolhimento parcial quando falta capacidade ou existe item não suportado.
- Em B, **Devolver tudo à origem** devolve as pilhas do recipiente de origem do
  item selecionado; outras origens permanecem na bolsa.
- O baú especial explica sua função no desafio original. Continua vazio e sem
  ações habilitadas nesta prévia: a custódia dinâmica do desafio não foi simulada.
- As ações por unidade, pilha e quantidade, os pesos, os caminhos, o relógio,
  o efeito da barreira e os checkpoints anteriores continuam disponíveis.

## Verificação

- **249 grupos sintéticos**, frente aos 227 da base: +22 grupos.
- **Sete grupos de validação de logs**, builds sem avisos/erros e **15/15 etapas**
  da bateria completa, incluindo os dois relatórios do baú/diálogo e integração Godot.
- Manifesto final de código:
  `.local/temple-validation-20261020-201201-11e165f2/manifest.json`.
- Marcadores `TAKE_ALL_UI_OK` e `RETURN_ALL_UI_OK` passaram sobre os dois itens
  originais do baú superior. Os testes exercitam repetição, conteúdo novamente
  disponível, janela recriada e callback anterior ao carregamento de checkpoint.
- Retirada/devolução/restauração conservam o checkpoint completo, incluindo
  explosivo consumido, cenário substituído, personagem, XP e portas.
- Capturas 1024×768 verificadas para coleta e bolsa; runners isolados aprovados,
  desktop de entrada `Default` preservado. A execução gráfica final completa
  em 1280×720 também passou, sem erros nos logs e sem mudar o desktop de entrada:
  `.local/continuation-final-isolated.json`, `Passed=true`, `LogFailure=null`.
- Os 120 links locais existentes na documentação foram conferidos antes desta
  consolidação, sem destinos ausentes.

## Limite concreto encontrado no NPC

O programa actemvil usa índice local 16 em Node012a (offsets 14504/14528), mas
tanto patch quanto master declaram 16 entradas (0–15). No CE o acesso é limitado
pelo vetor do mapa, não pelo bloco individual da instância nessas chamadas.
Isso não prova crash ou interferência em outra instância; exige compreender a
alocação e o ramo efetivamente executado. Nenhuma contagem foi alterada.

O ramo Speech requer tag e inteligência efetiva para a opção identificada; sua
condição aceita tag OU sucesso da rolagem. A porta tem ciclo próprio ligado a
locais e variável 20 do mapa. Esses fatos permanecem analisados, sem habilitar
conversa, custódia, combate geral ou passagem por um atalho presumido.

Próximo marco do NPC: definir o contrato de estados suportados e recusas de
acessos fora dele, com checkpoint separado para locais/mapa/globais. Modelo
recomendado: **GPT-6.1 Sol, esforço Alto**.

`Assets/`, a formatação preexistente de `TemplePreview.cs` e os `.uid` locais
foram preservados fora dos commits. Nenhum asset proprietário, dump ou captura
foi adicionado ao Git. Evidências copiáveis ficam no pacote de saída desta bateria.
