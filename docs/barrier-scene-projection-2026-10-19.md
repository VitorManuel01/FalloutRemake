# Marco 57 — projeção de cenário e navegação

`TempleBarrierProjection.Create` reconfere a proposta contra a sessão atual antes
de montar uma alternativa isolada de cenário/navegação. Recusa campos alterados,
revisão/token antigos, uso não vencido ou já consumido, GUID vazio e reutilização
do GUID de um uso. O chamador fornece a nova identidade; a projeção não a registra
globalmente, não a aplica à sessão e não promete sua persistência.

`ProjectedScenery` separa GUID novo, GUID causador e chave original substituída.
O cenário candidato ocupa o hex 14322/elevação 1/rotação 0 do objeto destruído,
com PID 33555365 e FID 33555376, independentemente da rotação/hex do explosivo.
Não fabrica um registro MAP ou reutiliza o ID original 44 para a nova instância.

A navegação alternativa oculta somente o bloqueador identificado. O substituto
verificado não bloqueia nem cria saída. Demais paredes, portas e passagens usam
o estado atual da sessão no instante da projeção. O grafo resultante é um snapshot:
alterações posteriores em portas não o modificam; uma futura aplicação precisará
revalidar/reconstruir a geometria, e não instalar um grafo antigo diretamente.

O relatório `--barrier-effect-report` agora usa esta implementação: após coleta
real e posicionamento a distância 1, a alternativa passa de 1245 a 1258 hexes
alcançáveis a partir de 29097, com seis saídas para a elevação 2. Na sessão viva,
a barreira continua fechada, a unidade continua posicionada/vencida e nenhum
checkpoint, XP ou byte original muda. O cenário novo ainda não é renderizado.

Validação: 210 grupos sintéticos, incluindo identidade/posição, campos forjados,
token após load, conclusão do uso, preservação dos demais obstáculos/saídas e
independência do snapshot de portas. O ensaio real confirma a passagem projetada.

Próximo: checkpoint novo para substituições e commit atômico de efeito/consumo;
depois conectar a mesma projeção à visualização. GPT-6.1 Sol, esforço Alto.
