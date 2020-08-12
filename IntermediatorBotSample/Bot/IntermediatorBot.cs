using IntermediatorBotSample.CommandHandling;
using IntermediatorBotSample.Model;
using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Activity = Microsoft.Bot.Schema.Activity;

namespace IntermediatorBotSample.Bot
{
    public class IntermediatorBot : IBot
    {
        private static readonly string[] _menuActividades =
        {
            "tarjetas",
            "agente",
            "cuentas",
            "reclamos"
        };
        private readonly string[] _cards =
        {
            Path.Combine(".", "Resources", "votar.json")
        };
        public BotState _conversationState;
        public BotState _userState;
        private readonly IConfiguration Configuracion;
        public IntermediatorBot(ConversationState conversationState, UserState userState, IConfiguration configuration)
        {
            Configuracion = configuration;
            _conversationState = conversationState;
            _userState = userState;
        }
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken ct)
        {
            var conversationStateAccessors = _conversationState.CreateProperty<Conversacion>(nameof(Conversacion));
            var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new Conversacion());
            var userStateAccessors = _userState.CreateProperty<Conversacion>(nameof(Conversacion));
            var conversacion = await userStateAccessors.GetAsync(turnContext, () => new Conversacion());
            Command showOptionsCommand = new Command(Commands.ShowOptions);
            string canalorigen = turnContext.Activity.ChannelId;
            string usuariorigen = turnContext.Activity.From.Name;
            HeroCard heroCard;
            if (canalorigen.ToLower().Equals("msteams"))
            {
                heroCard = new HeroCard()
                {
                    Title = $"Hola {usuariorigen}!",
                    Subtitle = "Bienvenido al servicio de gestión de consultas",
                    Text = $"Mi propósito es aprender de la psicohistoria y seguir las 3 reglas de Asimov, pero tambíen ayudarte a atender a tus clientes",
                    Buttons = new List<CardAction>()
                    {
                        new CardAction()
                        {
                            Title = "Esto es lo que puedo hacer: ",
                            Value = showOptionsCommand.ToString(),
                            Type = ActionTypes.ImBack
                        }
                    }
                };

                Activity replyActivity = turnContext.Activity.CreateReply();
                replyActivity.Attachments = new List<Attachment>() { heroCard.ToAttachment() };
                await turnContext.SendActivityAsync(replyActivity);
            }
            else
            {
                string replyText = "";
                var respuesta = turnContext.Activity.Text;
                if (!string.IsNullOrEmpty(respuesta))
                {
                    if (!string.IsNullOrEmpty(conversacion.Eleccion))
                    {
                        conversacion.Cambio = false;
                        switch (conversacion.Eleccion)
                        {
                            case "tarjetas":
                            case "cuentas":
                            case "reclamos":
                                await ValidarYEnviarEscenario(turnContext, conversacion, ct);
                                if (!conversacion.Cambio && !respuesta.ToLower().Equals("menu") && !respuesta.ToLower().Equals("preguntas") && !respuesta.ToLower().Equals("/salir"))
                                    replyText = await ConsultarBotAsync(turnContext, conversacion);
                                break;
                            case "agente":
                                await ValidarYEnviarEscenario(turnContext, conversacion, ct);
                                if (!conversacion.Cambio && !respuesta.ToLower().Equals("menu") && !respuesta.ToLower().Equals("preguntas") && !respuesta.ToLower().Equals("/salir"))
                                    await turnContext.SendActivityAsync("Derivando a agente");
                                break;
                            case "preguntas":
                                if (turnContext.Activity.Text.Equals("Excelente") || turnContext.Activity.Text.Equals("Bueno") || turnContext.Activity.Text.Equals("Regular")
                                || turnContext.Activity.Text.Equals("Malo") || turnContext.Activity.Text.Equals("Terrible"))
                                {
                                    await turnContext.SendActivityAsync("Gracias por tu respuesta, seguiremos mejorando!");
                                }
                                else
                                {
                                    await ValidarYEnviarEscenario(turnContext, conversacion, ct);
                                    if (!conversacion.Cambio && !respuesta.ToLower().Equals("menu") && !respuesta.ToLower().Equals("preguntas") && !respuesta.ToLower().Equals("/salir"))
                                        replyText = await ConsultaQnA(turnContext);
                                }
                                break;
                        }
                    }
                    else
                    {
                        switch (respuesta)
                        {
                            case "tarjetas":
                            case "cuentas":
                            case "preguntas":
                            case "reclamos":
                                conversacion.Eleccion = respuesta;
                                await GuardarCambios(turnContext, ct);
                                await turnContext.SendActivityAsync($"Perfecto, ahora estás comunicado con el área de {respuesta}, cómo puedo ayudarte?");
                                break;
                            case "menu":
                                await turnContext.SendActivityAsync(MostrarMenuInicial(turnContext, ct, conversacion).Result, ct);
                                break;
                            case "agente":
                                conversacion.Eleccion = respuesta;
                                await GuardarCambios(turnContext, ct);
                                break;
                            default:
                                replyText = await ConsultaQnA(turnContext);
                                break;
                        }

                    }
                    if (!string.IsNullOrEmpty(replyText) && !conversacion.Cambio)
                        await turnContext.SendActivityAsync(MessageFactory.Text(replyText, replyText), ct);
                }
                else
                {
                    if (!conversacion.Menu)
                    {
                        await GuardarCambios(turnContext, ct);
                        await turnContext.SendActivityAsync(MostrarMenuInicial(turnContext, ct, conversacion).Result, ct);
                    }
                }

            }

        }

        private async Task GuardarCambios(ITurnContext turnContext, CancellationToken ct)
        {
            await _conversationState.SaveChangesAsync(turnContext, false, ct);
            await _userState.SaveChangesAsync(turnContext, false, ct);
        }

        private async Task ValidarYEnviarEscenario(ITurnContext turnContext, Conversacion conversacion, CancellationToken cancellationToken)
        {
            string respuesta = turnContext.Activity.Text;

            if (_menuActividades.Contains(respuesta.ToLower()))
            {
                conversacion.Eleccion = respuesta.ToLower();
                conversacion.Cambio = true;
                await GuardarCambios(turnContext, cancellationToken);
                
                await turnContext.SendActivityAsync($"Perfecto, ahora estás comunicado con el área de {respuesta}, cómo puedo ayudarte?");
                if(respuesta.ToLower()!="agente")
                    await ConsultarBotAsync(turnContext, conversacion);
            }
            switch (respuesta.ToLower())
            {
                case "menu":
                    await turnContext.SendActivityAsync(MostrarMenuInicial(turnContext, cancellationToken, conversacion).Result, cancellationToken);
                    break;
                case "preguntas":
                    conversacion.Eleccion = respuesta.ToLower();
                    await GuardarCambios(turnContext, cancellationToken);
                    await turnContext.SendActivityAsync($"Perfecto, ahora estás comunicado con el área de {respuesta}, cómo puedo ayudarte?");
                    break;
                case "/salir":
                    conversacion.Eleccion = "preguntas";
                    conversacion.Token = conversacion.Conv = null;
                    conversacion.Cambio = true;
                    conversacion.Menu = false;
                    await GuardarCambios(turnContext, cancellationToken);
                    await turnContext.SendActivityAsync($"Gracias {turnContext.Activity.From.Name}, acá estaré cuando me necesites.");
                    var cardAttachment = CreateAdaptiveCardAttachment(_cards[0]);
                    await turnContext.SendActivityAsync(MessageFactory.Attachment(cardAttachment), cancellationToken);
                    break;
            }
            await GuardarCambios(turnContext, cancellationToken);

        }
        private static Attachment CreateAdaptiveCardAttachment(string filePath)
        {
            var adaptiveCardJson = File.ReadAllText(filePath);
            var adaptiveCardAttachment = new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(adaptiveCardJson),
            };
            return adaptiveCardAttachment;
        }
        public async Task<Activity> MostrarMenuInicial(ITurnContext turnContext, CancellationToken ct, Conversacion conv)
        {
            var reply = MessageFactory.Text($"Hola ¿De qué deseas hablar?, siempre puedes volver a ver estas opciones escribiendo \"menu\" sin comillas");
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                },
            };
            foreach (var item in _menuActividades)
            {
                reply.SuggestedActions.Actions.Add(new CardAction()
                {
                    Title = item,
                    Value = item.ToLower(),
                    Type = ActionTypes.ImBack
                });
            }
            reply.SuggestedActions.Actions.Add(new CardAction()
            {
                Title = "preguntas",
                Value = "preguntas",
                Type = ActionTypes.ImBack
            });
            conv.Menu = true;
            await GuardarCambios(turnContext, ct);
            return reply;
        }


        public async Task<string> ConsultarBotAsync(ITurnContext turnContext, Conversacion conv)
        {
            string token = "", conversacion = "";
            string idconversacion, URI, HtmlResult;
            JObject textoresult;
            var DirectToken = Configuracion[$"Llaves:{conv.Eleccion}"];
            if (conv.Cambio)
            {
                conv.Token = conv.Conv = null;
                conv.Cambio = false;
            }
            using (WebClient wc = new WebClient())
            {
                //inicializa en almacenamiento en memoria si es que el token y el id de la conversación no ha sido generado
                if (string.IsNullOrEmpty(conv.Token) || string.IsNullOrEmpty(conv.Conv))
                {
                    //obtener token y conversacion
                    URI = "https://directline.botframework.com/v3/directline/conversations";
                    wc.Headers["Authorization"] = $"Bearer {DirectToken}";
                    wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                    try
                    {
                        HtmlResult = await wc.UploadStringTaskAsync(URI, "");
                        textoresult = JObject.Parse(HtmlResult);
                    
                        token = textoresult["token"].ToString();
                        conversacion = textoresult["conversationId"].ToString();
                        conv.Token = token;
                        conv.Conv = conversacion;
                    }
                    catch (Exception)
                    {
                        return "REF:01 - Acabo de encontrar que me sobró un perno y algo anda mal, por favor intentalo más tarde...";

                        throw;
                    }
                }
                else
                {
                    token = conv.Token;
                    conversacion = conv.Conv;
                }
                var numRand = new Random();
                //enviar mensaje al bot
                var pregunta = new
                {
                    type = "message",
                    from = new
                    {
                        id = turnContext.Activity.From.Id,
                        name = turnContext.Activity.From.Name
                    },
                    text = turnContext.Activity.Text
                };
                var dataString = Newtonsoft.Json.JsonConvert.SerializeObject(pregunta);
                URI = $"https://directline.botframework.com/v3/directline/conversations/{conversacion}/activities";
                wc.Headers["Authorization"] = $"Bearer {token}";
                wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                try
                {
                    HtmlResult = await wc.UploadStringTaskAsync(URI, dataString);
                    textoresult = JObject.Parse(HtmlResult);
                }
                catch (Exception)
                {
                    return "REF:01 - Acabo de encontrar que me sobró un perno y algo anda mal, por favor intentalo más tarde...";
                    throw;
                }
                var arrayid = textoresult["id"].ToString().Split("|");
                idconversacion = arrayid[1].ToString();

                //recibir mensaje del bot
                URI = $"https://directline.botframework.com/v3/directline/conversations/{conversacion}/activities?watermark={idconversacion}";
                wc.Headers["Authorization"] = $"Bearer {token}";
                wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                try
                {
                    HtmlResult = await wc.DownloadStringTaskAsync(URI);
                    textoresult = JObject.Parse(HtmlResult);

                }
                catch (Exception)
                {
                    return "REF:01 - Acabo de encontrar que me sobró un perno y algo anda mal, por favor intentalo más tarde...";
                    throw;
                }
                JObject textoArray = (JObject)(textoresult.SelectToken("activities") as JArray).First();
                var resupuesta = textoArray.Value<string>("text");

                if (!_menuActividades.Contains(turnContext.Activity.Text.ToLower()))
                {
                    return resupuesta;
                }
                return "";
            }
        }

        public async Task<string> ConsultaQnA(ITurnContext context)
        {
            string respuesta;
            using (WebClient wc = new WebClient())
            {
                string URI = "https://<botespecializado>.azurewebsites.net/qnamaker/knowledgebases/<id-kb>/generateAnswer";

                var pregunta = new { question = $"{context.Activity.Text.ToLower()}" };
                var dataString = Newtonsoft.Json.JsonConvert.SerializeObject(pregunta);
                string HtmlResult = "";
                wc.Headers["Authorization"] = $"EndpointKey {Configuracion["Llaves:preguntas"]}";
                wc.Headers[HttpRequestHeader.ContentType] = "application/json; charset=utf-8";
                try
                {
                    HtmlResult = await wc.UploadStringTaskAsync(URI, dataString);
                }
                catch (Exception)
                {
                    return "REF:01 - Acabo de encontrar que me sobró un perno y algo anda mal, por favor intentalo más tarde...";
                    throw;
                }
                var textoresult = JObject.Parse(HtmlResult);
                JObject rr = (JObject)(textoresult.SelectToken("answers") as JArray).First();
                respuesta = rr.Value<string>("answer");
                if (respuesta.Equals("No good match found in KB."))
                {
                    respuesta = "Como todo gran humano... digo, robot, sigo aprendiendo, todavía no puedo responder a esa pregunta, pero tal vez la respuesta es 42";
                }
            }
            if (!_menuActividades.Contains(context.Activity.Text.ToLower()))
            {
                return respuesta;
            }
            return null;
        }
    }
}
