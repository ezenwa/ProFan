# Installation and usage / Instalación y uso

## English

### Requirements

- ASUS ProArt PX13 HN7306.
- Windows 11 x64.
- ASUS System Control Interface / ATKACPI installed.
- Administrator access.

### Controls

- **20–100%:** applies the selected constant fan curve.
- **Return to Automatic:** restores the ASUS profile active before manual control.
- **Notification-area icon:** right-click for quick presets, status, Open, or Exit.
- **Open ProFan:** restores the main window centered in the working area of the screen containing the pointer.
- **Close window:** preserves the current mode and keeps ProFan available in the notification area.
- **Start with Windows (minimized):** creates an elevated logon task so ProFan starts automatically in the notification area without a UAC prompt. Manual launches still open the main window.
- **Check for updates:** checks the latest GitHub release; ProFan also checks automatically at startup and alerts only when a newer version is available.
- **Exit:** restores Automatic and terminates ProFan.
- **Lid close, suspend, or hibernate:** temporarily restores ASUS Automatic. If manual mode was active, ProFan reapplies the same percentage after resume; if recovery fails, it remains in Automatic.

The percentage is a firmware request and may not map linearly to physical RPM.

While manual control is active, ProFan maintains both fan curves every two seconds and recovers the manual performance endpoint if another ASUS service changes it. The selection remains active until you choose **Return to Automatic**, except for safety restoration on suspend, sign-out, full Exit, upgrade, or uninstall. Avoid running another fan-control utility at the same time.

## Español

### Requisitos

- ASUS ProArt PX13 HN7306.
- Windows 11 x64.
- ASUS System Control Interface / ATKACPI instalado.
- Acceso de administrador.

### Controles

- **20–100%:** aplica la curva constante seleccionada.
- **Volver a Automático:** restaura el perfil ASUS anterior.
- **Icono del área de notificación:** clic derecho para porcentajes, estado, Abrir o Salir.
- **Abrir ProFan:** restaura la ventana principal centrada en el área útil de la pantalla donde está el puntero.
- **Cerrar ventana:** conserva el modo actual y mantiene ProFan en el área de notificación.
- **Iniciar con Windows (minimizado):** crea una tarea de inicio de sesión elevada para que ProFan arranque automáticamente en el área de notificación sin mostrar UAC. Las aperturas manuales siguen mostrando la ventana principal.
- **Buscar actualizaciones:** consulta la release más reciente en GitHub; ProFan también lo hace automáticamente al iniciar y solo avisa si existe una versión superior.
- **Salir:** restaura Automático y finaliza ProFan.
- **Cerrar la tapa, suspender o hibernar:** restaura temporalmente Automático de ASUS. Si estaba activo el modo manual, ProFan reaplica el mismo porcentaje al reanudar; si la recuperación falla, permanece en Automático.

El porcentaje es una solicitud al firmware y puede no corresponder linealmente con las RPM físicas.

Mientras el control manual está activo, ProFan mantiene ambas curvas cada dos segundos y recupera el perfil manual si otro servicio ASUS lo cambia. La selección permanece activa hasta elegir **Volver a Automático**, excepto por la restauración de seguridad al suspender, cerrar sesión, usar Salir, actualizar o desinstalar. Evita ejecutar simultáneamente otra utilidad de control de ventiladores.
