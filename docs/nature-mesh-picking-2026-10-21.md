# Seleção da geometria Nature

O clique nas rochas e troncos importados agora consulta os triângulos do modelo,
inclusive acima da altura das formas provisórias. As formas de consulta são
compartilhadas por malha e usam exatamente a transformação visual de cada
instância. Permanecem filhas do **mesmo corpo original**, com os mesmos IDs,
metadados e camada de seleção; não criam entidades de gameplay.

Com Nature ativo, o volume provisório é desativado para a consulta do clique.
Ao desligar a opção, ele volta ao estado anterior e as formas de triângulo
ficam inativas. Os filtros continuam controlando a camada do corpo original.
A navegação por hexágonos não lê essas formas e seus bloqueios não mudam.

`NATURE_PICKING_OK` lança uma consulta física contra um triângulo alto do tronco,
confere o corpo/ID original, desliga Nature para comprovar que a região alta
deixa de selecionar o antigo volume curto e reativa a consulta. O checkpoint
é comparado antes/depois. Build sem avisos/erros; **319 grupos sintéticos**,
**19/19 etapas**, `.local/temple-validation-20261021-112129-b43aaa1d/manifest.json`.

Próximo incremento: guardar a opção Nature nas preferências visuais locais,
com leitura compatível do formato anterior. **GPT-6.1 Sol, esforço Alto**.
