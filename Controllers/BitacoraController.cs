using BitacorasAPI.Models;
using BitacorasAPI.Services;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace BitacorasAPI.Controllers
{
    [RoutePrefix("api/bitacora")]
    public class BitacoraController : ApiController
    {
        private readonly SecurityService _securityService = new SecurityService();

        private string GetAccessToken()
        {
            return Request.Headers.Authorization?.Parameter;
        }

        private string GetRefreshToken()
        {
            IEnumerable<string> refreshTokenValues;
            if (Request.Headers.TryGetValues("X-Refresh-Token", out refreshTokenValues))
            {
                return refreshTokenValues.FirstOrDefault();
            }
            return null;
        }

        // =========================================================
        // ============ ENDPOINT DE BUSQUEDA (FILTROS OPCIONALES) ===
        // =========================================================
        [HttpGet]
        [Route("search")]
        public async Task<IHttpActionResult> SearchBitacoras(int? usuario = null, DateTime? fecha = null)
        {
            //1.validar tokens
            var accessToken = GetAccessToken();
            var refreshToken = GetRefreshToken();
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                return Unauthorized();

            bool isValid = await _securityService.ValidateTokenAsync(accessToken);
            if (!isValid)
            {
                var newTokens = await _securityService.RefreshTokenAsync(refreshToken);
                if (newTokens == null ||
                    string.IsNullOrEmpty(newTokens.AccessToken) ||
                    string.IsNullOrEmpty(newTokens.RefreshToken))
                {
                    return Unauthorized();
                }
                accessToken = newTokens.AccessToken;
                refreshToken = newTokens.RefreshToken;
            }

            //2.construir la consulta con filtros opcionales
            var bitacoras = new List<object>();  // Cambio aquí a 'object' para no incluir el IdBitacora
            var connectionString = ConfigurationManager.ConnectionStrings["MySqlConnection"].ConnectionString;

            try
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string sql = @"
                SELECT id_bitacora, fecha_bitacora, id_usuario, descripcion
                FROM bitacoras
                WHERE 1=1
            ";

                    var parameters = new List<MySqlParameter>();

                    if (usuario.HasValue)
                    {
                        sql += " AND id_usuario = @usuario ";
                        parameters.Add(new MySqlParameter("@usuario", usuario.Value));
                    }

                    if (fecha.HasValue)
                    {
                        sql += " AND DATE(fecha_bitacora) = @fecha ";
                        parameters.Add(new MySqlParameter("@fecha", fecha.Value.Date));
                    }

                    sql += " ORDER BY fecha_bitacora DESC";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        foreach (var p in parameters)
                            cmd.Parameters.Add(p);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var bitacora = new
                                {
                                    FechaBitacora = reader.GetDateTime("fecha_bitacora"),
                                    IdUsuario = reader.GetInt32("id_usuario"),
                                    Descripcion = reader.GetString("descripcion")
                                };
                                bitacoras.Add(bitacora);
                            }
                        }
                    }
                }

                //si no se encontraron bitacoras, devolver mensaje
                if (bitacoras.Count == 0)
                {
                    var responseMessage = new { Mensaje = "No hay bitácoras para mostrar." };
                    var response = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                    response.Headers.Add("X-New-Access-Token", accessToken);
                    response.Headers.Add("X-New-Refresh-Token", refreshToken);
                    return ResponseMessage(response);
                }

                var responseWithBitacoras = Request.CreateResponse(HttpStatusCode.OK, bitacoras);
                responseWithBitacoras.Headers.Add("X-New-Access-Token", accessToken);
                responseWithBitacoras.Headers.Add("X-New-Refresh-Token", refreshToken);
                return ResponseMessage(responseWithBitacoras);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =========================================================
        // ============ CREAR BITACORA (SIN RESTAR HORAS) ===========
        // =========================================================
        [HttpPost]
        [Route("")]
        public async Task<IHttpActionResult> CreateBitacora([FromBody] BitacoraRequest request)
        {
            var accessToken = GetAccessToken();
            var refreshToken = GetRefreshToken();
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                return Unauthorized();

            bool isValid = await _securityService.ValidateTokenAsync(accessToken);
            if (!isValid)
            {
                var newTokens = await _securityService.RefreshTokenAsync(refreshToken);
                if (newTokens == null ||
                    string.IsNullOrEmpty(newTokens.AccessToken) ||
                    string.IsNullOrEmpty(newTokens.RefreshToken))
                {
                    return Unauthorized();
                }
                accessToken = newTokens.AccessToken;
                refreshToken = newTokens.RefreshToken;
            }

            if (request == null)
                return BadRequest("El cuerpo de la solicitud está vacío.");
            if (request.IdUsuario <= 0)
                return BadRequest("El ID del usuario no es válido.");
            if (string.IsNullOrWhiteSpace(request.Descripcion))
                return BadRequest("La descripción no puede estar vacía.");

            //se toma la hora local del servidor tal cual
            var fechaLocal = DateTime.Now;

            var connectionString = ConfigurationManager.ConnectionStrings["MySqlConnection"].ConnectionString;
            try
            {
                BitacoraResponse nuevaBitacora = null;
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    //insertar la fechaBitacora manualmente
                    string sqlInsert = @"
                        INSERT INTO bitacoras (fecha_bitacora, id_usuario, descripcion)
                        VALUES (@fecha, @idUsuario, JSON_OBJECT('accion', @desc));
                        SELECT LAST_INSERT_ID();
                    ";
                    int idInsertado = 0;
                    using (var cmd = new MySqlCommand(sqlInsert, conn))
                    {
                        cmd.Parameters.AddWithValue("@fecha", fechaLocal);
                        cmd.Parameters.AddWithValue("@idUsuario", request.IdUsuario);
                        cmd.Parameters.AddWithValue("@desc", request.Descripcion);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                idInsertado = Convert.ToInt32(reader[0]);
                        }
                    }

                    string sqlSelect = @"
                        SELECT id_bitacora, fecha_bitacora, id_usuario, descripcion
                        FROM bitacoras
                        WHERE id_bitacora = @id
                    ";
                    using (var cmd2 = new MySqlCommand(sqlSelect, conn))
                    {
                        cmd2.Parameters.AddWithValue("@id", idInsertado);
                        using (var reader2 = cmd2.ExecuteReader())
                        {
                            if (reader2.Read())
                            {
                                nuevaBitacora = new BitacoraResponse
                                {
                                    IdBitacora = reader2.GetInt32("id_bitacora"),
                                    FechaBitacora = reader2.GetDateTime("fecha_bitacora"),
                                    IdUsuario = reader2.GetInt32("id_usuario"),
                                    Descripcion = reader2.GetString("descripcion")
                                };
                            }
                        }
                    }
                }

                if (nuevaBitacora == null)
                    return InternalServerError(new Exception("No se pudo recuperar la bitácora recién creada."));

                var response = Request.CreateResponse(HttpStatusCode.Created, nuevaBitacora);
                response.Headers.Add("X-New-Access-Token", accessToken);
                response.Headers.Add("X-New-Refresh-Token", refreshToken);
                return ResponseMessage(response);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =========================================================
        // ============ OBTENER TODAS LAS BITACORAS ================
        // =========================================================
        [HttpGet]
        [Route("")]
        public async Task<IHttpActionResult> GetBitacoras()
        {
            var accessToken = GetAccessToken();
            var refreshToken = GetRefreshToken();
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                return Unauthorized();

            bool isValid = await _securityService.ValidateTokenAsync(accessToken);
            if (!isValid)
            {
                var newTokens = await _securityService.RefreshTokenAsync(refreshToken);
                if (newTokens == null ||
                    string.IsNullOrEmpty(newTokens.AccessToken) ||
                    string.IsNullOrEmpty(newTokens.RefreshToken))
                {
                    return Unauthorized();
                }
                accessToken = newTokens.AccessToken;
                refreshToken = newTokens.RefreshToken;
            }

            var bitacoras = new List<BitacoraResponse>();
            var connectionString = ConfigurationManager.ConnectionStrings["MySqlConnection"].ConnectionString;
            try
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string sql = @"
                        SELECT id_bitacora, fecha_bitacora, id_usuario, descripcion
                        FROM bitacoras
                        ORDER BY fecha_bitacora DESC
                    ";
                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            bitacoras.Add(new BitacoraResponse
                            {
                                IdBitacora = reader.GetInt32("id_bitacora"),
                                FechaBitacora = reader.GetDateTime("fecha_bitacora"),
                                IdUsuario = reader.GetInt32("id_usuario"),
                                Descripcion = reader.GetString("descripcion")
                            });
                        }
                    }
                }

                var response = Request.CreateResponse(HttpStatusCode.OK, bitacoras);
                response.Headers.Add("X-New-Access-Token", accessToken);
                response.Headers.Add("X-New-Refresh-Token", refreshToken);
                return ResponseMessage(response);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}
