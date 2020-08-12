
# Bot Derivador #

![.NET Core: master](https://github.com/jyapurv/botderivador/workflows/.NET%20Core/badge.svg?branch=master)
**No se recomienda usar este proyecto en producción.**
# Consideraciones:

 1. La derivación solo funciona en un modelo Request-->Response eso
    significa que el bot no puede enviar un mensaje al mamifero de forma
    proactiva, cada interacción debe ser iniciada con un request.
 2. La derivación solo funciona con una sola respuesta por request, si el contexto envía más de una solo será tomada en cuenta la primera.
 3. El menú, manejo de elección y llaves se manejan de forma serparada en el String Array, Application Properties y Switch Case.
 4. No ha pasado ningún control de pruebas funcionales, unitarias ni de seguridad.
 
 # Importante:
- Deberás crear un archivo appsettings.json dentro del directorio RouterBot con la siguiente estructura:
```json
{
  "MicrosoftAppId": "",
  "MicrosoftAppPassword": "",
  //Configura las llaves de DirectChannel, QnA y Luis
  "Llaves": {
    "agente": "",
    "cuentas": "",
    "tarjetas": "",
    "preguntas": ""
  }
  }

```
