namespace ServidorWeb.Models
{
    public enum EstadoProceso
    {
        Disponible,
        Ocupado,
        Procesando
    }

    public class Proceso
    {
        public string Id { get; set; }

        public EstadoProceso Estado { get; set; } =
            EstadoProceso.Disponible;

        public int? IdSolicitudActual { get; set; }
        public int Progreso { get; set; }
        public bool Ocupado { get; set; }
         
    }
}

