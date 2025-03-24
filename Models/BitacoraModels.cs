using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BitacorasAPI.Models
{
    public class BitacoraRequest
    {
        public int IdUsuario { get; set; }

        //esta propiedad contiene la accion + JSON
        public string Descripcion { get; set; }
    }

    public class BitacoraResponse
    {
        public int IdBitacora { get; set; }
        public DateTime FechaBitacora { get; set; }
        public int IdUsuario { get; set; }

        //se recupera como un string (aunque internamente es un JSON en MySQL).
        public string Descripcion { get; set; }
    }
}