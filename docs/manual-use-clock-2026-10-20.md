# Tempo manual do ensaio — 20/10/2026

Depois de posicionar o explosivo, **Tempo · ensaio** abre os prazos de todos os
explosivos posicionados nas áreas visitadas. **+1 s** e **+10 s** avançam a fila
existente; os frames e a caminhada continuam sem avançá-la automaticamente.

O avanço para exatamente no primeiro vencimento, inclusive quando o prazo tem
fração de segundo. A parcela não aplicada é descartada; não existe avanço
automático posterior. Um evento vencido permanece posicionado e a interface
passa a **Tempo · pendente**, com os dois avanços bloqueados até sua resolução.
Este marco não consome o item nem altera cenário ou criaturas.

A janela lista mapa, elevação, prazo exato e estado. A consulta funciona ao
inspecionar outra elevação, mas o avanço exige a elevação do personagem e ator
parado. Outros modais, passos fora do conjunto permitido e callbacks de uma
janela fechada ou de uma sessão anterior são recusados. Carregar/reconstruir fecha
a janela; checkpoint v4 preserva evento, GUID e prazo, sem novo formato.

O teste `USE_CLOCK_UI_OK` usa a unidade original coletada e posicionada pela
interface. Confere 7,5 → 6,5 → 0 segundos, parada exata ao solicitar 10 segundos,
evento pendente sem efeito/consumo, recarga ainda bloqueada e recusas por
movimento, inspeção, troca de modal, passo inválido e callback antigo. Compara
o checkpoint inteiro, aceitando somente a diferença esperada de ticks.

Corrigida também a limpeza dos rótulos removidos da árvore antes de projetá-los
na câmera, observada ao atualizar o objeto imediatamente após um avanço.
Build, testes sintéticos, bateria completa e captura isolada são registrados
nos artefatos locais. Nenhum asset original foi adicionado ao Git.

Build sem avisos/erros; 225 grupos sintéticos aprovados. A integração específica
e a captura em 1024×768 passaram, com saída de erro vazia e desktop preservado.
A primeira bateria completa falhou depois dos marcadores do relógio, no teste
preexistente de gravações consecutivas das preferências visuais. Manifesto:
`.local/temple-validation-20261020-104101-6a558412/manifest.json`. Essa ocorrência
recorrente será tratada separadamente, antes de concluir a próxima bateria.

Próximo marco: consultar e confirmar a resolução restrita da barreira do Templo,
com revalidação do perfil e do plano. GPT-6.1 Sol, esforço Alto.
