# Quarto marco — navegação inicial, 17/10/2026 (America/Bahia)

Branch: `milestone/temple-navigation`, a partir de `88da5e7`. Os ajustes já existentes
do editor (UIDs, recurso C# e indentação) foram preservados no commit separado `827cc58`.
Dados Epic, Godot 4.7.2 .NET, alvo net8.0 e referência CE permanecem os confirmados.

## Verificado

- Compilação: **zero erros e zero avisos**.
- **30 grupos sintéticos**: 22 anteriores e 8 novos. Cobrem tipo FID diferente do PID,
  flags/elevação, multihex e bordas, objetos sobrepostos/IDs repetidos, inventário,
  portas e estado inconsistente, desvios, destino inacessível, saídas terminais,
  interpolação do marcador, isolamento da rota e conversão dos 40.000 hexes.
- Dados reais: entrada 28906 livre; 1206 hexes bloqueados; 540 alcançáveis;
  31 hexes de saída, 13 alcançáveis; zero desacordos de flags de porta.
- Teste Godot: clique projetado pela câmera até 24508; **23 passos adjacentes**,
  todos livres, posição final conferida. Porta 24308 recusada como destino.
- Teste de saída: 31306 em **12 passos**, sem permitir continuar por ela.
- Ocultar paredes/criaturas não altera passabilidade. Trocar elevação remove o
  marcador; retornar/reiniciar restaura a entrada. Movimento normal por frames
  também exercitado, além da verificação passo a passo.
- MAP reconstruído byte a byte antes e depois; fonte não é modificada.
- Capturas 1440×900 revisadas: entrada, seleção e percurso. Interface e marcador
  legíveis. Teste gráfico D3D12/Forward+ na RTX 5060 Ti concluiu com saída 0 e sem
  mensagens de erro após a correção de recursos descrita abaixo.

## Correção durante a validação

As primeiras execuções gráficas completaram o percurso, mas falharam ao encerrar,
em finalizadores de objetos C#. Passamos a compartilhar malhas/formas por categoria
e liberar explicitamente referências gerenciadas de recursos temporários no thread
principal. Os nós mantêm suas referências nativas. Recursos persistentes/UI são
liberados ao sair da árvore. Três execuções gráficas seguintes fecharam com código 0.
Contexto: [ciclo de vida de RefCounted no Godot](https://docs.godotengine.org/en/stable/classes/class_refcounted.html).
Isso registra a correção observada no projeto, sem atribuir uma causa comprovada ao motor.

## Reproduzir

```powershell
dotnet build 'Fallout Remake.csproj'
dotnet run --project tools/TempleInspector -- --self-test
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --navigation
# Use o $godotExe confirmado no README:
& $godotExe --headless --path . -- --preview-test
& $godotExe --path . -- --preview-test --capture .local/temple-walk.png
```

Sucesso: `PREVIEW_TEST_OK` e `NAVIGATION_TEST_OK`, saída 0. Relatório local:
`.local/navigation-inspection.json`; capturas `.local/temple-walk*.png` e logs
`.local/navigation-render*.log`. Nenhum desses arquivos é versionado.

O teste chama as ações da cena e converte coordenadas de câmera; não substitui uma
sessão manual completa de cliques nem compara a imagem com o jogo original.
Limites e próximo passo em [navigation.md](navigation.md).
