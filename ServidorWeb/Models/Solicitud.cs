namespace ServidorWeb.Models
{
    public enum TipoSolicitud
    {
        Entrada,
        Salida
    }

    public enum EstadoSolicitud
    {
        EnEspera,
        Procesando,
        Completada,
        Fallida,
        TimeOut
    }

    public class Solicitud
    {
        public EstadoSolicitud Estado { get; set; } =
            EstadoSolicitud.EnEspera;

        public DateTime FechaLLegada { get; set; } = DateTime.Now;
        public DateTime? InicioAtencion { get; set; }
        public DateTime? FinAtencion { get; set; }
        public int TiempoProcesoSegundos { get; set; }
        /// <summary>
        /// Total de tiempo en cola
        /// </summary>
        public double totalEsperaSegundos { get; set; }
        /// <summary>
        /// total tiempo de procesamiento
        /// </summary>
        public double totalTiempoProcesamiento { get; set; }
        /// <summary>
        /// Total tiempo de procesamiento cola + proceso
        /// </summary>
        public double totalTiempo { get; set; }
        public int Progreso { get; set; }
        public string IdSolicitud { get; set; }
        public string IdProceso { get; set; }
        public string? MensajeError { get; set; }
        public int Id { get; set; }
    }
}
