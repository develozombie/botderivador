using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IntermediatorBotSample.Model
{
    public class Conversacion
    {
        //eleccion inicial
        public string Eleccion { get; set; }
        //token de servicio
        public string Token { get; set; }
        //id de conversacion
        public string Conv { get; set; }
        //referencia al cambio de intent de conversación
        public bool Cambio { get; set; }

        public bool Menu { get; set; }
    }
}
