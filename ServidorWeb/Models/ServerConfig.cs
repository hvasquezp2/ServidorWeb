using System.Security.Cryptography;

namespace ServidorWeb.Models
{
    public class ServerConfig
    {
        public int MaxTrabajadores { get; set; } = 5;
        public int CapacidadSolicitudes { get; set; } = 20;
        public int TiempoMaximoProcesamientoMs { get; set; } = 8;
        public int TiempoMinimoProcesamientoMs { get; set; } = 5;
        public int TiempoMaximoEspera { get; set; } = 16;

    }
}
