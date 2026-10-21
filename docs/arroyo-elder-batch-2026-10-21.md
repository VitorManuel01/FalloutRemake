# Bateria: entrada de Arroyo e anciã

Branch `codex/arroyo-elder-entry-audit`, continuação de `codex/nature-assets`.
As pedras e árvores Nature anteriores continuam presentes. Esta bateria não
adicionou modelos, texturas, texto proprietário ou assets de outra categoria.

Nove marcos concluídos:

1. **Abertura original auditada** (`2942a12`): identidade por offset/SID,
   declaração de 10 locais, guardas, MSG e arestas. Detectados G47 inicial50
   e locais10/11 fora da declaração em outros ramos.
2. **Entrada neutra persistente** (`7d02a5a`): karma15, reputação65,
   Wanderer/G41 e 600XP na mesma chegada, checkpointv9 e migração do recibov8.
   Retorno/carga não repetem os efeitos; adulterações preservam a sessão viva.
3. **Ir até a anciã** (`a428938`): caminhada real ao hex vizinho, identidade
   correta apesar de outro ID20, Esc, K/R, retomada e callbacks antigos.
4. **Consulta original** (`4c5e648`): quatro respostas e caminhos limitados,
   reinício/cancelamento/fim, texto da instalação e opções de entrega de item
   desabilitadas. A partida permanece idêntica.
5. **Leitura de atributos PRO** (`8629871`): 35 bases/35 bônus/18 perícias,
   ordem serializada, palavra opcional e cópias próprias. Carisma da anciã
   base5/bônus0, sem presumir um sistema completo de personagem.
6. **Reação neutra inicial auditada** (`594958d`): projeção32, inicialização
   do programa, guardas e contribuições separadas, sem executar INT.
7. **Ensaio separado de estado** (`5bc02ae`): reação32→22 no caminho direto,
   32→12 na ponte dupla ou Node013; dez locais próprios e 16 globais limitados.
8. **Conversa repetida auditada** (`873b638`): Node004, L7 e estado de área
   Vault13/11, distintos do histórico de IDs de mapas.
9. **Recusa de fontes alteradas**: cinco cenários com cópias privadas de INT,
   scripts.lst, GAM e PRO, incluindo mudança entre leituras. A fonte original
   continua aceita ao final; nenhum arquivo da instalação é escrito.

Validações finais:

- **335 grupos sintéticos e 20/20 etapas**, builds Godot .NET/inspetor sem
  avisos/erros: `.local/temple-validation-20261021-162703-4598b77f/manifest.json`.
- Auditorias de reação/estado, conversa repetida e cinco recusas adicionais:
  `.local/arroyo-elder-final-audit.json`, build do inspetor sem avisos/erros.
- Capturas em desktop isolado aprovadas para entrada, caminhada e consulta:
  `.local/arroyo-globals-v9-isolated.json`, `.local/arroyo-elder-isolated.json`,
  `.local/arroyo-elder-consultation-isolated.json`. Desktop de entrada
  **Default antes e depois**, sem interferir nas janelas do usuário.

Uso na prévia: concluir o percurso do templo, atravessar para Arroyo, usar
**Ir até a anciã** e depois **Anciã · conversa em consulta**. Karma/reputação
aparecem no painel; **K/R** salva/carrega a partida. A consulta não concede
itens, escreve progresso de diálogo, reproduz cabeça/áudio ou aplica reação
ao NPC ativo. O ensaio de estado continua separado, acessível pelo relatório.

Os ajustes de espaço preexistentes de `TemplePreview.cs` e os `.uid` locais
foram preservados. Nada enviado ao remoto.

Próximo marco: estender o ensaio separado para Node004 no contexto neutro
(Vault13 desconhecido, L7=0), incluindo guarda e resultado de uma segunda
conversa, antes de ligar o estado do diálogo à partida.
**Modelo recomendado: GPT-6.1 Sol, esforço Alto.**

Detalhes: [abertura](arroyo-elder-opening-audit-2026-10-21.md),
[entrada v9](arroyo-entry-globals-v9-2026-10-21.md),
[caminhada](arroyo-elder-approach-2026-10-21.md),
[consulta](arroyo-elder-consultation-2026-10-21.md),
[PRO](arroyo-elder-prototype-stats-2026-10-21.md),
[reação](arroyo-elder-neutral-reaction-2026-10-21.md),
[estado separado](arroyo-elder-detached-state-2026-10-21.md),
[conversa repetida](arroyo-elder-repeat-audit-2026-10-21.md).
