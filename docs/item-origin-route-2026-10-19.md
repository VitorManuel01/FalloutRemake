# Marco 38 — planejar a volta ao recipiente

`ItemOriginRoute.Find` calcula uma rota para o recipiente de um item carregado,
incluindo passagens entre áreas já visitadas e entre elevações. O resultado
separa cada caminhada da transição seguinte. A consulta não carrega áreas,
não altera posição/portas/bolsa e não executa scripts. Só aceita uma origem
que ainda exista na bolsa, identificada por mapa, proprietário e chave fonte.

O custo é passos mais uma unidade por passagem explícita. A busca considera
as portas atuais, hexes de saída terminais, destinos válidos e áreas em cache.
Recusa passagens ambíguas, ocultas, bloqueadas ou que exijam recarregar a mesma
elevação. Visitas repetidas não geram ciclos infinitos. O trecho final termina
num hex sem saída ao alcance do recipiente. Não abre portas automaticamente.

Validação: compilação sem avisos/erros e 153 grupos sintéticos aprovados.
Os novos cenários cobrem duas transições (área e elevação), checkpoint,
identidade obsoleta, caminhada em curso, área intermediária desconhecida,
ciclos e saídas sem suporte. O relatório real é reproduzível com:

```powershell
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --item-origin-route-report
```

Ele recolhe a unidade original de Healing Powder ID 106 numa sessão de teste,
visita o exterior e calcula a volta. Exige que o checkpoint serializado seja
idêntico antes/depois da consulta e reconstrói os dois MAPs byte a byte.

Próximo: ligar o primeiro trecho à bolsa. Enter continuará necessário para cada
passagem; a devolução também continuará explícita. GPT-6.1 Sol, esforço Alto.
