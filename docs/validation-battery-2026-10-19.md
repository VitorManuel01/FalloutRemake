# Marco 48 — bateria local reproduzível sem janela

`tools/Validate-Temple.ps1` reúne onze etapas: build da prévia, build do inspector,
testes sintéticos, sete relatórios reais e integração Godot com `--headless`.
Os caminhos de Godot e dados são obrigatórios; não procura outras instalações.

```powershell
& .\tools\Validate-Temple.ps1 `
  -GodotExe 'D:\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' `
  -DataDirectory 'D:\Games\fallout2\Fallout 2 English'
```

Cada execução cria uma pasta única em `.local/temple-validation-*`, com stdout,
stderr e manifesto JSON contendo código de saída, duração e aprovação de cada
etapa. Os relatórios devem confirmar a reconstrução da fonte. A integração deve
emitir os marcadores essenciais, incluindo os novos testes de ícones e elevação.
Uma falha interrompe as etapas seguintes e registra a causa, com saída 1.

O timeout padrão é 120 segundos por etapa, configurável por `-TimeoutSeconds`.
Ele pode encerrar somente o processo criado para a etapa que excedeu esse prazo.
Não toca instâncias do usuário. Godot sempre recebe `--headless`, sem fallback
gráfico. Para capturas, continua valendo o runner de desktop isolado já existente.
Não execute simultaneamente com outra suíte da prévia: os slots de teste são
compartilhados entre validadores, embora separados dos dados reais do usuário.

Validação original do marco 48: dez etapas aprovadas, cerca de 24 segundos no total,
163 grupos sintéticos, todos os seis relatórios reais e integração completa.
O teste encontrou e corrigiu a necessidade de reter o handle do processo no
Windows PowerShell para registrar corretamente o código de saída.

Na revisão do marco 49, uma execução falhou por acesso a arquivo no teste de
preferências. O runner registrou saída 1 na terceira etapa e não iniciou o Godot.
A repetição completa passou com 167 grupos. A causa do acesso negado não foi
reproduzida; ambos os manifestos foram mantidos. Falhas sintéticas agora preservam
também o stack trace, facilitando investigar uma recorrência sem mascará-la.

Próximo: revisar os registros desta bateria e resolver as questões documentadas
do explosivo antes de implementar seu uso. GPT-6.1 Sol, esforço Alto.
