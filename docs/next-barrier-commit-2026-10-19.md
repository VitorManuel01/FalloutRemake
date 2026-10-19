# Retomada após o marco 59

Recomendação: GPT-6.1 Sol, esforço Alto. Próximo incremento: aplicar somente o
efeito confirmado da barreira, com persistência e consumo atômico. Não ligar
combate geral, armadilhas, scripts arbitrários ou tempo de parede neste recorte.

Estado atual: checkpoint v3 conserva carregado/posicionado/consumido; o relógio
lógico para no primeiro prazo e devolve sobra; a proposta da barreira exige uso
vencido, perfis verificados e distância até 3 na mesma elevação. A projeção cria
identidade nova e grafo isolado. Nada substitui a barreira na sessão viva.

## Contrato a implementar

- Novo checkpoint v4 com lista obrigatória de substituições por área; v1–v3
  migram para lista vazia. Registros novos precisam de campos JSON obrigatórios.
- Guardar GUID próprio, GUID do uso causador, chave MAP substituída, posição do
  cenário original e PID/FID/hash do novo protótipo. Validar unicidade global e
  não reutilizar GUID de uso nem o ID original 44 como identidade da instância.
- O causador precisa existir e estar consumido; cada barreira só pode ser
  substituída uma vez. Usos consumidos legados podem existir sem substituição.
- O PRO 33555365 não pertence ao conjunto original de protótipos MAP. Resolver
  explicitamente pelo catálogo local e conferir seu hash ao carregar. Não
  adicionar bytes de PRO/FRM ao checkpoint ou ao Git.
- Prevalidar proposta, token, revisão, prazo, perfil, raio, identidades e capacidade
  do registro antes de consumir ou substituir. Falhas preservam o mundo inteiro.
- Restaurar portas, itens e substituições antes de validar posições de atores e
  usos: a geometria aberta pode autorizar posições bloqueadas no MAP original.
  Construir uma sessão candidata completa antes de trocar a sessão ativa.
- Integrar a sobreposição em todos os consumidores de navegação, inclusive
  `TempleTravel.CanArrive`, rotas, saídas, checkpoint e cache da prévia. Revalidar
  e reconstruir a geometria; não instalar um snapshot de projeção antigo.
- Na visualização, ocultar apenas a forma original e criar o substituto em
  camada própria. Preservar objeto/fonte original e sua contagem, metadados e bytes.

## Aceitação

Repetição, proposta concorrente, token antigo, protótipo alterado, GUID repetido,
save/load em área inativa e JSON incompleto não duplicam cenário nem consomem
outra unidade. A unidade usada conserva `original = restante + carregado +
posicionado + consumido`. Não há XP de destruição neste trecho do script.

O ensaio real já disponível caminha/coleta/posiciona no hex 14521. O efeito deve
abrir 14322/elevação 1, expandir 1245 → 1258 hexes e permitir as seis saídas para
elevação 2. Chegada 23914 alcança 974 hexes e o baú 376 com duas unidades estáticas;
o baú 100 com script continua indisponível. Validar ida, retorno e checkpoint.

Rodar a bateria de doze etapas e captura somente pelo runner isolado. Não usar
o desktop ativo nem interferir nas instâncias do usuário. Todos os `.uid` não
rastreados preexistentes devem continuar preservados.
