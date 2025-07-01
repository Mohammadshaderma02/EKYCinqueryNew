using DocumentFormat.OpenXml.Office2010.Word;
using EkycInquiry.Models;
using EkycInquiry.Models.DataTable;
using EkycInquiry.Models.ViewModel;
using EkycInquiry.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using NpgsqlTypes;
using Seq.Api;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EkycInquiry.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private WidContext WidContext { get; set; }
        private EkycClient EkycClient { get; set; }
        public IConfiguration Config { get; set; }

        public HomeController(WidContext widContext, EkycClient client, IConfiguration config)
        {
            WidContext = widContext;
            EkycClient = client;
            Config = config;
        }

        [HttpPost]
        public async Task<IActionResult> GetSummaryData([FromQuery] string? FromStr, [FromQuery] string? ToStr)
        {
            try
            {
                DateTime? From, To;
                if (FromStr == null)
                    From = DateTime.Now.AddDays(-30); // Default to last 30 days instead of Dec 2024
                else
                    From = DateTime.ParseExact(FromStr, "dd-MM-yyyy", CultureInfo.InvariantCulture);
                if (ToStr == null)
                    To = DateTime.Now;
                else
                    To = DateTime.ParseExact(ToStr, "dd-MM-yyyy", CultureInfo.InvariantCulture);

                From = DateTime.SpecifyKind(From.Value, DateTimeKind.Utc);
                To = DateTime.SpecifyKind(To.Value, DateTimeKind.Utc);

                // Ensure we don't go beyond one month to maintain performance
                if ((To.Value - From.Value).TotalDays > 31)
                {
                    From = To.Value.AddDays(-31);
                }

                // PERFORMANCE OPTIMIZED: Limited to one month with proper indexing
                var summaryData = await WidContext.Database.SqlQuery<SummaryDataResult>($$$"""
                    WITH date_range AS (
                        SELECT {{{From}}} as from_date, {{{To.Value.AddDays(1)}}} as to_date
                    ),
                    session_stats AS (
                        SELECT 
                            status,
                            COUNT(*) as session_count,
                            DATE("creationTime") as session_date,
                            CASE WHEN "ocrData" ? 'code' THEN 'non_jordanian' ELSE 'jordanian' END as nationality_type
                        FROM zain."Session", date_range
                        WHERE "creationTime" >= date_range.from_date 
                        AND "creationTime" <= date_range.to_date
                        GROUP BY status, DATE("creationTime"), nationality_type
                    ),
                    workflow_stats AS (
                        SELECT 
                            s.uid,
                            s.status,
                            st."index",
                            CASE 
                                WHEN st."index" + 1 = 1 THEN 'scan_barcode'
                                WHEN st."index" + 1 = 2 THEN 'sim_selection'
                                WHEN st."index" + 1 = 3 THEN 'policy'
                                WHEN st."index" + 1 = 4 THEN 'document'
                                WHEN st."index" + 1 = 5 THEN 'ocr_summary'
                                WHEN st."index" + 1 = 6 THEN 'self_recording'
                                WHEN st."index" + 1 = 7 THEN 'identification_completed'
                                WHEN st."index" + 1 = 8 THEN 'package_selection'
                                WHEN st."index" + 1 = 9 THEN 'payment'
                                ELSE 'unknown'
                            END AS "lastCompletedStep",
                            ROW_NUMBER() OVER (PARTITION BY s.uid ORDER BY st."index" DESC) as rn
                        FROM zain."Session" s, date_range
                        INNER JOIN zain."SessionWorkflow" sw ON sw."sessionId" = s.id
                        INNER JOIN zain."SessionStep" st ON st."sessionWorkflowId" = sw.id
                        WHERE s."creationTime" >= date_range.from_date 
                        AND s."creationTime" <= date_range.to_date
                        AND st.completed = true
                    )
                    SELECT 
                        (SELECT json_agg(json_build_object('status', status, 'count', sum(session_count))) 
                         FROM session_stats GROUP BY status) as sessions_count,
                        (SELECT json_agg(json_build_object('lastCompletedStep', "lastCompletedStep", 'count', count(*))) 
                         FROM workflow_stats WHERE rn = 1 GROUP BY "lastCompletedStep") as steps_count,
                        (SELECT json_build_object(
                            'JordanianCustomers', count(*) FILTER (WHERE nationality_type = 'jordanian'),
                            'NonJordanianCustomers', count(*) FILTER (WHERE nationality_type = 'non_jordanian')
                         ) FROM session_stats) as nationality_comparison,
                        (SELECT json_agg(json_build_object('session_date', session_date, 
                            'approved_count', sum(session_count) FILTER (WHERE status = 'approved'),
                            'approval_pending_count', sum(session_count) FILTER (WHERE status = 'approval_pending'),
                            'to_discard_count', sum(session_count) FILTER (WHERE status = 'to_discard'),
                            'working_count', sum(session_count) FILTER (WHERE status = 'working'),
                            'total_count', sum(session_count)
                         )) FROM session_stats GROUP BY session_date ORDER BY session_date) as time_series_report
                    """).FirstOrDefaultAsync();

                // Separate optimized query for flow and channel data
                var flowChannelData = await WidContext.Database.SqlQuery<FlowChannelResult>($$$"""
                    WITH date_range AS (
                        SELECT {{{From}}} as from_date, {{{To}}} as to_date
                    )
                    SELECT 
                        (SELECT json_agg(json_build_object('flow', l.flow, 'count', count(*)))
                         FROM "zain-custom"."Line" l, date_range
                         INNER JOIN zain."Session" s ON s.uid = l.uid
                         WHERE s.status IN ('approved', 'approval_pending') 
                         AND s."creationTime" >= date_range.from_date 
                         AND s."creationTime" <= date_range.to_date
                         GROUP BY l.flow) as flow_count,
                        (SELECT json_build_object(
                            'eKYCFlow', count(DISTINCT s.id) FILTER (WHERE w."sessionId" IS NOT NULL),
                            'IntegrationFlow', count(DISTINCT s.id) FILTER (WHERE w."sessionId" IS NULL)
                         ) FROM zain."Session" s, date_range
                         LEFT JOIN zain."SessionWorkflow" w ON w."sessionId" = s.id
                         WHERE s."creationTime" >= date_range.from_date 
                         AND s."creationTime" <= date_range.to_date) as channel_comparisons
                    """).FirstOrDefaultAsync();

                return Ok(new
                {
                    Status = 0,
                    SessionsCount = JsonConvert.DeserializeObject(summaryData.sessions_count),
                    StepsCount = JsonConvert.DeserializeObject(summaryData.steps_count),
                    NationalityComparison = JsonConvert.DeserializeObject(summaryData.nationality_comparison),
                    FlowCount = JsonConvert.DeserializeObject(flowChannelData.flow_count),
                    TimeSeriesReport = JsonConvert.DeserializeObject(summaryData.time_series_report),
                    ChannelComparisons = JsonConvert.DeserializeObject(flowChannelData.channel_comparisons)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetSummaryData: {ex.Message}");
                return Ok(new { Status = -1 });
            }
        }
        // MAJOR OPTIMIZATION: Completely rewritten GetIndexData method with proper pagination and filtering
        [HttpPost]
        public async Task<IActionResult> GetIndexData([FromForm] AjaxPostModel Model)
        {
            try
            {
                int pageNum = Model.start;
                int pageSize = Model.length;
                string globalSearch = Model?.search?.value?.Trim();

                // Extract column-specific search values
                var columnSearches = new Dictionary<string, string>();
                if (Model.columns != null)
                {
                    for (int i = 0; i < Model.columns.Count; i++)
                    {
                        var columnSearch = Model.columns[i]?.search?.value?.Trim();
                        if (!string.IsNullOrEmpty(columnSearch))
                        {
                            columnSearches[Model.columns[i].data] = columnSearch;
                        }
                    }
                }

                DateTime oneMonthAgoUtc = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-30), DateTimeKind.Utc);

                using var connection = WidContext.Database.GetDbConnection();
                await connection.OpenAsync();

                var parameters = new List<(string name, object value)>
        {
            ("@oneMonthAgo", oneMonthAgoUtc),
            ("@PageSize", pageSize),
            ("@Offset", pageNum)
        };

                var whereClauses = new List<string>
        {
            @"s.""ocrData"" IS NOT NULL",
            @"jsonb_typeof(s.""ocrData"") = 'object'",
            @"s.""ocrData"" != '{}'::jsonb",
            @"s.""creationTime"" >= @oneMonthAgo"
        };

                // Global search across all searchable columns
                if (!string.IsNullOrEmpty(globalSearch))
                {
                    var globalSearchParam = $"%{globalSearch}%";
                    parameters.Add(("@globalSearch", globalSearchParam));

                    whereClauses.Add(@"
                (
                    COALESCE(s.""ocrData"" ->> 'userGivenNames', s.""ocrData"" ->> 'givenNames') ILIKE @globalSearch OR
                    s.""ocrData"" ->> 'userPersonalNumber' ILIKE @globalSearch OR
                    s.""ocrData"" ->> 'documentNumber' ILIKE @globalSearch OR
                    s.""ocrData"" ->> 'nationality' ILIKE @globalSearch OR
                    s.""uid""::text ILIKE @globalSearch OR
                    s.""status"" ILIKE @globalSearch OR
                    T.""userCreationEmail"" ILIKE @globalSearch
                )");
                }

                // Column-specific searches
                var paramCounter = 1;
                foreach (var columnSearch in columnSearches)
                {
                    var paramName = $"@colSearch{paramCounter}";
                    var searchValue = $"%{columnSearch.Value}%";
                    parameters.Add((paramName, searchValue));

                    var columnClause = columnSearch.Key.ToLower() switch
                    {
                        "name" => $"COALESCE(s.\"ocrData\" ->> 'userGivenNames', s.\"ocrData\" ->> 'givenNames') ILIKE {paramName}",
                        "document_id" => $"s.\"ocrData\" ->> 'userPersonalNumber' ILIKE {paramName}",
                        "session_id" => $"s.\"uid\"::text ILIKE {paramName}",
                        "status" => $"s.\"status\" ILIKE {paramName}",
                        "channel" => $"T.\"userCreationEmail\" ILIKE {paramName}",
                        "nationality" => $"s.\"ocrData\" ->> 'nationality' ILIKE {paramName}",
                        _ => null
                    };

                    if (!string.IsNullOrEmpty(columnClause))
                    {
                        whereClauses.Add(columnClause);
                    }
                    paramCounter++;
                }

                string whereClause = string.Join(" AND ", whereClauses);

                // Build the base query
                string baseQuery = $@"
            SELECT s.id, s.uid, 
                COALESCE(s.""ocrData"" ->> 'userGivenNames', s.""ocrData"" ->> 'givenNames') as name,
                s.""ocrData"" ->> 'userPersonalNumber' as document_id,
                s.""ocrData"" ->> 'nationality' as nationality,
                s.""faceMatchThreshold"",
                s.""livenessThreshold"",
                s.""tokenId"",
                s.""creationTime"",
                s.""status"",
                sss.""uid"" AS latest_step_uid,
                T.""userCreationEmail"" as channel_user
            FROM zain.""Session"" s
            INNER JOIN zain.""Token"" T ON T.id = s.""tokenId""
            LEFT JOIN (
                SELECT DISTINCT ON (sw.""sessionId"") ss.uid, sw.""sessionId""
                FROM zain.""SessionStep"" ss
                INNER JOIN zain.""SessionWorkflow"" sw ON sw.id = ss.""sessionWorkflowId""
                WHERE ss.active = true
                ORDER BY sw.""sessionId"", ss.id DESC
            ) sss ON sss.""sessionId"" = s.id
            WHERE {whereClause}";

                // Get total count efficiently
                var countQuery = $"SELECT COUNT(*) FROM ({baseQuery}) AS count_alias";
                using var countCmd = connection.CreateCommand();
                countCmd.CommandText = countQuery;
                foreach (var param in parameters.Where(p => !p.name.Contains("PageSize") && !p.name.Contains("Offset")))
                {
                    var p = countCmd.CreateParameter();
                    p.ParameterName = param.name;
                    p.Value = param.value;
                    countCmd.Parameters.Add(p);
                }
                var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

                // Data query with proper LIMIT and OFFSET
                var dataQuery = $@"
            {baseQuery}
            ORDER BY s.id DESC
            LIMIT @PageSize OFFSET @Offset";

                using var dataCmd = connection.CreateCommand();
                dataCmd.CommandText = dataQuery;
                foreach (var param in parameters)
                {
                    var p = dataCmd.CreateParameter();
                    p.ParameterName = param.name;
                    p.Value = param.value;
                    dataCmd.Parameters.Add(p);
                }

                var results = new List<string[]>();
                using var reader = await dataCmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32("id");
                    var uid = reader.GetString("uid");
                    var name = reader.IsDBNull("name") ? "" : reader.GetString("name");
                    var documentId = reader.IsDBNull("document_id") ? "" : reader.GetString("document_id");
                    var nationality = reader.IsDBNull("nationality") ? "" : reader.GetString("nationality");
                    var tokenId = reader.IsDBNull("tokenId") ? 0 : reader.GetInt32("tokenId");
                    var creationTime = reader.GetDateTime("creationTime").AddHours(3).ToString("G");
                    var status = reader.IsDBNull("status") ? "" : reader.GetString("status");
                    var currentStep = reader.IsDBNull("latest_step_uid") ? "" : reader.GetString("latest_step_uid");
                    var channelUser = reader.IsDBNull("channel_user") ? "" : reader.GetString("channel_user");

                    // Get channel name using the existing method
                    var channelName = GetChannelName(channelUser);

                    var actionData = $"data-id='{id}' data-token-id='{tokenId}' data-session-id='{uid}'";

                    results.Add(new[]
                    {
                name,
                documentId,
                uid,
                nationality,
                $"<a {actionData} data-type='OCRFile' href='#' class='btn btn-sm btn-outline-primary'>OCR File</a>",
                $"<a {actionData} data-type='LivenesssOutput' href='#' class='btn btn-sm btn-outline-info'>Liveness</a>",
                $"<a {actionData} data-type='AuditContent' href='#' class='btn btn-sm btn-outline-secondary'>Audit</a>",
                $"<a {actionData} data-type='LineInformation' href='#' class='btn btn-sm btn-outline-success'>Line Info</a>",
                $"<a {actionData} data-type='Media' href='#' class='btn btn-sm btn-outline-warning'>Media</a>",
                $"<a {actionData} data-type='SessionSteps' href='#' class='btn btn-sm btn-outline-dark'>Steps</a>",
                currentStep,
                channelName,
                $"<a href='http://192.168.190.36/#/events?filter={uid}' target='_blank' class='btn btn-sm btn-outline-danger'>Logs</a>",
                creationTime,
                GetStatusBadge(status)
            });
                }

                return Ok(new AjaxResponseModel
                {
                    data = results.ToArray(),
                    draw = Model.draw,
                    recordsTotal = totalCount.ToString(),
                    recordsFiltered = totalCount.ToString()
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetIndexData: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        // Helper method to generate status badges
        private string GetStatusBadge(string status)
        {
            return status?.ToLower() switch
            {
                "approved" => "<span class='badge bg-success'>Approved</span>",
                "approval_pending" => "<span class='badge bg-warning'>Pending</span>",
                "to_discard" => "<span class='badge bg-danger'>Rejected</span>",
                "working" => "<span class='badge bg-info'>Working</span>",
                _ => $"<span class='badge bg-secondary'>{status}</span>"
            };
        }

        // Enhanced AjaxPostModel to support column searching
        public class AjaxPostModel
        {
            public int draw { get; set; }
            public int start { get; set; }
            public int length { get; set; }
            public SearchModel search { get; set; }
            public List<ColumnModel> columns { get; set; }
        }

        public class SearchModel
        {
            public string value { get; set; }
            public bool regex { get; set; }
        }

        public class ColumnModel
        {
            public string data { get; set; }
            public string name { get; set; }
            public bool searchable { get; set; }
            public bool orderable { get; set; }
            public SearchModel search { get; set; }
        }

        // MAJOR OPTIMIZATION: Completely rewritten GetIndexData method with one month limit
        //[HttpPost]
        //public async Task<IActionResult> GetIndexData([FromForm] AjaxPostModel Model)
        //{
        //    try
        //    {
        //        int PageNum = Model.start;
        //        int PageSize = Model.length;
        //        string search = Model?.search?.value?.Trim();

        //        DateTime oneMonthAgoUtc = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-30), DateTimeKind.Utc);

        //        using var connection = WidContext.Database.GetDbConnection();
        //        await connection.OpenAsync();

        //        var parameters = new List<(string name, object value)>
        //{
        //    ("@oneMonthAgo", oneMonthAgoUtc),
        //    ("@PageSize", PageSize),
        //    ("@PageNum", PageNum)
        //};

        //        var whereClauses = new List<string>
        //{
        //    @"s.""ocrData"" IS NOT NULL",
        //    @"jsonb_typeof(s.""ocrData"") = 'object'",
        //    @"s.""ocrData"" != '{}'::jsonb",
        //    @"s.""creationTime"" >= @oneMonthAgo"
        //};

        //        if (!string.IsNullOrEmpty(search))
        //        {
        //            var searchParam = $"%{search}%";
        //            parameters.Add(("@search", searchParam));

        //            // Use individual JSONB fields (indexable) and avoid COALESCE
        //            whereClauses.Add(@"
        //        (
        //            s.""ocrData"" ->> 'userGivenNames' ILIKE @search OR
        //            s.""ocrData"" ->> 'givenNames' ILIKE @search OR
        //            s.""ocrData"" ->> 'userPersonalNumber' ILIKE @search OR
        //            s.""ocrData"" ->> 'documentNumber' ILIKE @search OR
        //            s.""ocrData"" ->> 'nationality' ILIKE @search OR
        //            s.""uid""::text ILIKE @search OR
        //            T.""userCreationEmail"" ILIKE @search
        //        )");
        //        }
        //        string whereClause = string.Join(" AND ", whereClauses);

        //        string baseQuery = $@"
        //    SELECT s.id, s.uid, 
        //        COALESCE(s.""ocrData"" ->> 'userGivenNames', s.""ocrData"" ->> 'givenNames') as name,
        //        s.""ocrData"" ->> 'userPersonalNumber' as document_id,
        //        s.""ocrData"" ->> 'nationality' as nationality,
        //        s.""faceMatchThreshold"",
        //        s.""livenessThreshold"",
        //        s.""tokenId"",
        //        s.""creationTime"",
        //        s.""status"",
        //        sss.""uid"" AS latest_step_uid,
        //        T.""userCreationEmail"" as channel_user
        //    FROM zain.""Session"" s
        //    INNER JOIN zain.""Token"" T ON T.id = s.""tokenId""
        //    LEFT JOIN (
        //        SELECT DISTINCT ON (sw.""sessionId"") ss.uid, sw.""sessionId""
        //        FROM zain.""SessionStep"" ss
        //        INNER JOIN zain.""SessionWorkflow"" sw ON sw.id = ss.""sessionWorkflowId""
        //        WHERE ss.active = true
        //        ORDER BY sw.""sessionId"", ss.id DESC
        //    ) sss ON sss.""sessionId"" = s.id
        //    WHERE {whereClause}";

        //        // Count query
        //        var countQuery = $"SELECT COUNT(*) FROM ({baseQuery}) AS count_alias";
        //        using var countCmd = connection.CreateCommand();
        //        countCmd.CommandText = countQuery;
        //        foreach (var param in parameters.Where(p => p.name != "@PageSize" && p.name != "@PageNum"))
        //        {
        //            var p = countCmd.CreateParameter();
        //            p.ParameterName = param.name;
        //            p.Value = param.value;
        //            countCmd.Parameters.Add(p);
        //        }
        //        var totalCount = 1000; //Convert.ToInt32(await countCmd.ExecuteScalarAsync());

        //        // Data query with pagination
        //        var dataQuery = $@"
        //    {baseQuery}
        //    ORDER BY s.id DESC
        //    LIMIT @PageSize OFFSET @PageNum";

        //        using var dataCmd = connection.CreateCommand();
        //        dataCmd.CommandText = dataQuery;
        //        foreach (var param in parameters)
        //        {
        //            var p = dataCmd.CreateParameter();
        //            p.ParameterName = param.name;
        //            p.Value = param.value;
        //            dataCmd.Parameters.Add(p);
        //        }

        //        var results = new List<string[]>();
        //        using var reader = await dataCmd.ExecuteReaderAsync();

        //        while (await reader.ReadAsync())
        //        {
        //            var id = reader.GetInt32("id");
        //            var uid = reader.GetString("uid");
        //            var name = reader.IsDBNull("name") ? "" : reader.GetString("name");
        //            var documentId = reader.IsDBNull("document_id") ? "" : reader.GetString("document_id");
        //            var tokenId = reader.IsDBNull("tokenId") ? 0 : reader.GetInt32("tokenId");
        //            var creationTime = reader.GetDateTime("creationTime").AddHours(3).ToString("G");
        //            var status = reader.IsDBNull("status") ? "" : reader.GetString("status");
        //            var currentStep = reader.IsDBNull("latest_step_uid") ? "" : reader.GetString("latest_step_uid");
        //            var channelUser = reader.IsDBNull("channel_user") ? "" : reader.GetString("channel_user");

        //            // Get channel name using the existing method
        //            var channelName = GetChannelName(channelUser);

        //            var actionData = $"data-id='{id}' data-token-id='{tokenId}' data-session-id='{uid}'";

        //            results.Add(new[]
        //            {
        //        name,
        //        documentId,
        //        uid,
        //        $"<a {actionData} data-type='OCRFile' href='#'>See OCR File</a>",
        //        $"<a {actionData} data-type='LivenesssOutput' href='#'>See Liveness Output</a>",
        //        $"<a {actionData} data-type='AuditContent' href='#'>See Audit Content</a>",
        //        $"<a {actionData} data-type='LineInformation' href='#'>See Line Information</a>",
        //        $"<a {actionData} data-type='Media' href='#'>See Media</a>",
        //        $"<a {actionData} data-type='SessionSteps' href='#'>See Session Steps</a>",
        //        currentStep,
        //        channelName, // Added channel information
        //        $"<a href='http://192.168.190.36/#/events?filter={uid}' target='_blank'>Go To Logs</a>",
        //        creationTime,
        //        status
        //    });
        //        }

        //        return Ok(new AjaxResponseModel
        //        {
        //            data = results.ToArray(),
        //            draw = Model.draw,
        //            recordsTotal = totalCount.ToString(),
        //            recordsFiltered = totalCount.ToString()
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error in GetIndexData: {ex.Message}");
        //        return BadRequest(new { error = ex.Message });
        //    }
        //}

        //public async Task<IActionResult> GetIndexData([FromForm] AjaxPostModel Model)
        //{
        //    try
        //    {
        //        int PageNum = Model.start;
        //        int PageSize = Model.length;
        //        string search = Model?.search?.value?.Trim();

        //        DateTime oneMonthAgoUtc = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-30), DateTimeKind.Utc);

        //        using var connection = WidContext.Database.GetDbConnection();
        //        await connection.OpenAsync();

        //        var parameters = new List<(string name, object value)>
        //{
        //    ("@oneMonthAgo", oneMonthAgoUtc),
        //    ("@PageSize", PageSize),
        //    ("@PageNum", PageNum)
        //};

        //        var whereClauses = new List<string>
        //{
        //    @"s.""ocrData"" IS NOT NULL",
        //    @"jsonb_typeof(s.""ocrData"") = 'object'",
        //    @"s.""ocrData"" != '{}'::jsonb",
        //    @"s.""creationTime"" >= @oneMonthAgo"
        //};

        //        if (!string.IsNullOrEmpty(search))
        //        {
        //            var searchParam = $"%{search}%";
        //            parameters.Add(("@search", searchParam));

        //            // Use individual JSONB fields (indexable) and avoid COALESCE
        //            whereClauses.Add(@"
        //        (
        //            s.""ocrData"" ->> 'userGivenNames' ILIKE @search OR
        //            s.""ocrData"" ->> 'givenNames' ILIKE @search OR
        //            s.""ocrData"" ->> 'userPersonalNumber' ILIKE @search OR
        //            s.""ocrData"" ->> 'documentNumber' ILIKE @search OR
        //            s.""ocrData"" ->> 'nationality' ILIKE @search OR
        //            s.""uid""::text ILIKE @search
        //        )");
        //        }
        //        string whereClause = string.Join(" AND ", whereClauses);


        //        //COALESCE(s.""ocrData""->> 'userPersonalNumber', s.""ocrData""->> 'documentNumber') as document_id,
        //        string baseQuery = $@"
        //    SELECT s.id, s.uid, 
        //        COALESCE(s.""ocrData"" ->> 'userGivenNames', s.""ocrData"" ->> 'givenNames') as name,
        //        s.""ocrData"" ->> 'userPersonalNumber' as document_id,
        //        s.""ocrData"" ->> 'nationality' as nationality,
        //        s.""faceMatchThreshold"",
        //        s.""livenessThreshold"",
        //        s.""tokenId"",
        //        s.""creationTime"",
        //        s.""status"",
        //        sss.""uid"" AS latest_step_uid
        //    FROM zain.""Session"" s
        //    LEFT JOIN (
        //        SELECT DISTINCT ON (sw.""sessionId"") ss.uid, sw.""sessionId""
        //        FROM zain.""SessionStep"" ss
        //        INNER JOIN zain.""SessionWorkflow"" sw ON sw.id = ss.""sessionWorkflowId""
        //        WHERE ss.active = true
        //        ORDER BY sw.""sessionId"", ss.id DESC
        //    ) sss ON sss.""sessionId"" = s.id
        //    WHERE {whereClause}";

        //        // Count query
        //        var countQuery = $"SELECT COUNT(*) FROM ({baseQuery}) AS count_alias";
        //        using var countCmd = connection.CreateCommand();
        //        countCmd.CommandText = countQuery;
        //        foreach (var param in parameters.Where(p => p.name != "@PageSize" && p.name != "@PageNum"))
        //        {
        //            var p = countCmd.CreateParameter();
        //            p.ParameterName = param.name;
        //            p.Value = param.value;
        //            countCmd.Parameters.Add(p);
        //        }
        //        var totalCount =1000; //Convert.ToInt32(await countCmd.ExecuteScalarAsync());

        //        // Data query with pagination
        //        var dataQuery = $@"
        //    {baseQuery}
        //    ORDER BY s.id DESC
        //    LIMIT @PageSize OFFSET @PageNum";

        //        using var dataCmd = connection.CreateCommand();
        //        dataCmd.CommandText = dataQuery;
        //        foreach (var param in parameters)
        //        {
        //            var p = dataCmd.CreateParameter();
        //            p.ParameterName = param.name;
        //            p.Value = param.value;
        //            dataCmd.Parameters.Add(p);
        //        }

        //        var results = new List<string[]>();
        //        using var reader = await dataCmd.ExecuteReaderAsync();

        //        while (await reader.ReadAsync())
        //        {
        //            var id = reader.GetInt32("id");
        //            var uid = reader.GetString("uid");
        //            var name = reader.IsDBNull("name") ? "" : reader.GetString("name");
        //            var documentId = reader.IsDBNull("document_id") ? "" : reader.GetString("document_id");
        //            var tokenId = reader.IsDBNull("tokenId") ? 0 : reader.GetInt32("tokenId");
        //            var creationTime = reader.GetDateTime("creationTime").AddHours(3).ToString("G");
        //            var status = reader.IsDBNull("status") ? "" : reader.GetString("status");
        //            var currentStep = reader.IsDBNull("latest_step_uid") ? "" : reader.GetString("latest_step_uid");
        //            //var faceMatchThreshold = reader.IsDBNull("faceMatchThreshold") ? "N/A" : reader.GetString("faceMatchThreshold");
        //            //var livenessThreshold = reader.IsDBNull("livenessThreshold") ? "N/A" : reader.GetString("livenessThreshold");

        //            var actionData = $"data-id='{id}' data-token-id='{tokenId}' data-session-id='{uid}'";

        //            results.Add(new[]
        //            {
        //        name,
        //        documentId,
        //        uid,
        //        $"<a {actionData} data-type='OCRFile' href='#'>See OCR File</a>",
        //        $"<a {actionData} data-type='LivenesssOutput' href='#'>See Liveness Output</a>",
        //        //faceMatchThreshold,
        //        //livenessThreshold,
        //        $"<a {actionData} data-type='AuditContent' href='#'>See Audit Content</a>",
        //        $"<a {actionData} data-type='LineInformation' href='#'>See Line Information</a>",
        //        $"<a {actionData} data-type='Media' href='#'>See Media</a>",
        //        $"<a {actionData} data-type='SessionSteps' href='#'>See Session Steps</a>",
        //        currentStep,
        //        $"<a href='http://192.168.190.36/#/events?filter={uid}' target='_blank'>Go To Logs</a>",
        //        creationTime,
        //        status
        //    });
        //        }

        //        return Ok(new AjaxResponseModel
        //        {
        //            data = results.ToArray(),
        //            draw = Model.draw,
        //            recordsTotal = totalCount.ToString(),
        //            recordsFiltered = totalCount.ToString()
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error in GetIndexData: {ex.Message}");
        //        return BadRequest(new { error = ex.Message });
        //    }
        //}

        //public async Task<IActionResult> GetIndexData([FromForm] AjaxPostModel Model)
        //{
        //    try
        //    {
        //        int PageNum = Model.start;
        //        int PageSize = Model.length;
        //        string search = Model?.search?.value?.Trim();

        //        // PERFORMANCE: Limit to last 30 days
        //        DateTime oneMonthAgoUtc = DateTime.SpecifyKind(DateTime.Now.AddDays(-30), DateTimeKind.Utc);

        //        using var connection = WidContext.Database.GetDbConnection();
        //        await connection.OpenAsync();

        //        string baseQuery = @"
        //            SELECT s.id, s.uid, 
        //                COALESCE(s.""ocrData"" ->> 'userGivenNames', s.""ocrData"" ->> 'givenNames') as name,
        //                COALESCE(s.""ocrData"" ->> 'userPersonalNumber', s.""ocrData"" ->> 'documentNumber') as document_id,
        //                s.""ocrData"" ->> 'nationality' as nationality,
        //                s.""faceMatchThreshold"",
        //                s.""livenessThreshold"",
        //                s.""tokenId"",
        //                s.""creationTime"",
        //                s.""status"",
        //                sss.""uid"" AS latest_step_uid
        //            FROM zain.""Session"" s
        //            LEFT JOIN (
        //                SELECT DISTINCT ON (sw.""sessionId"") ss.uid, sw.""sessionId""
        //                FROM zain.""SessionStep"" ss
        //                INNER JOIN zain.""SessionWorkflow"" sw ON sw.id = ss.""sessionWorkflowId""
        //                WHERE ss.active = true
        //                ORDER BY sw.""sessionId"", ss.id DESC
        //            ) sss ON sss.""sessionId"" = s.id
        //            WHERE s.""ocrData"" IS NOT NULL 
        //            AND jsonb_typeof(s.""ocrData"") = 'object' 
        //            AND s.""ocrData"" != '{}'::jsonb
        //            AND s.""creationTime"" >= @oneMonthAgo";

        //        string whereClause = "";
        //        var parameters = new List<(string name, object value)>();
        //        parameters.Add(("@oneMonthAgo", oneMonthAgoUtc));

        //        if (!string.IsNullOrEmpty(search))
        //        {
        //            search = $"%{search}%";
        //            whereClause = @" AND (
        //                COALESCE(s.""ocrData"" ->> 'userGivenNames', s.""ocrData"" ->> 'givenNames') ILIKE @search OR
        //                COALESCE(s.""ocrData"" ->> 'userPersonalNumber', s.""ocrData"" ->> 'documentNumber') ILIKE @search OR
        //                s.""ocrData"" ->> 'nationality' ILIKE @search OR
        //                s.""uid""::text ILIKE @search OR
        //                sss.""uid""::text ILIKE @search
        //            )";
        //            parameters.Add(("@search", search));
        //        }

        //        // Get total count efficiently
        //        var countQuery = $"SELECT COUNT(*) FROM ({baseQuery} {whereClause}) as counted";
        //        using var countCmd = connection.CreateCommand();
        //        countCmd.CommandText = countQuery;
        //        foreach (var param in parameters)
        //        {
        //            var p = countCmd.CreateParameter();
        //            p.ParameterName = param.name;
        //            p.Value = param.value;
        //            countCmd.Parameters.Add(p);
        //        }
        //        var totalCount = 1000;//Convert.ToInt32(await countCmd.ExecuteScalarAsync());

        //        // Get paginated data
        //        var dataQuery = $"{baseQuery} {whereClause} ORDER BY s.id DESC LIMIT @PageSize OFFSET @PageNum";
        //        using var dataCmd = connection.CreateCommand();
        //        dataCmd.CommandText = dataQuery;

        //        foreach (var param in parameters)
        //        {
        //            var p = dataCmd.CreateParameter();
        //            p.ParameterName = param.name;
        //            p.Value = param.value;
        //            dataCmd.Parameters.Add(p);
        //        }

        //        var pageSizeParam = dataCmd.CreateParameter();
        //        pageSizeParam.ParameterName = "@PageSize";
        //        pageSizeParam.Value = PageSize;
        //        dataCmd.Parameters.Add(pageSizeParam);

        //        var pageNumParam = dataCmd.CreateParameter();
        //        pageNumParam.ParameterName = "@PageNum";
        //        pageNumParam.Value = PageNum;
        //        dataCmd.Parameters.Add(pageNumParam);
        //        //dataCmd.CommandTimeout = 9999999;
        //        var results = new List<string[]>();
        //        using var reader = await dataCmd.ExecuteReaderAsync();

        //        while (await reader.ReadAsync())
        //        {
        //            var id = reader.GetInt32("id");
        //            var uid = reader.GetString("uid").ToString();
        //            var name = reader.IsDBNull("name") ? "" : reader.GetString("name");
        //            var documentId = reader.IsDBNull("document_id") ? "" : reader.GetString("document_id");
        //            var tokenId = reader.IsDBNull("tokenId") ? 0 : reader.GetInt32("tokenId");
        //            var creationTime = reader.GetDateTime("creationTime").AddHours(3).ToString("G");
        //            var status = reader.IsDBNull("status") ? "" : reader.GetString("status");
        //            var currentStep = reader.IsDBNull("latest_step_uid") ? "" : reader.GetString("latest_step_uid").ToString();
        //            var faceMatchThreshold = reader.IsDBNull("faceMatchThreshold") ? "N/A" : reader.GetString("faceMatchThreshold").ToString();
        //            var livenessThreshold = reader.IsDBNull("livenessThreshold") ? "N/A" : reader.GetString("livenessThreshold").ToString();

        //            // Build action links efficiently
        //            var actionData = $"data-id='{id}' data-token-id='{tokenId}' data-session-id='{uid}'";

        //            results.Add(new string[]
        //            {
        //                name,
        //                documentId,
        //                uid,
        //                $"<a {actionData} data-type='OCRFile' href='#'>See OCR File</a>",
        //                $"<a {actionData} data-type='LivenesssOutput' href='#'>See Liveness Output</a>",
        //                faceMatchThreshold,
        //                livenessThreshold,
        //                $"<a {actionData} data-type='AuditContent' href='#'>See Audit Content</a>",
        //                $"<a {actionData} data-type='LineInformation' href='#'>See Line Information</a>",
        //                $"<a {actionData} data-type='Media' href='#'>See Media</a>",
        //                $"<a {actionData} data-type='SessionSteps' href='#'>See Session Steps</a>",
        //                currentStep,
        //                $"<a href='http://192.168.190.36/#/events?filter={uid}' target='_blank'>Go To Logs</a>",
        //                creationTime,
        //                status
        //            });
        //        }

        //        return Ok(new AjaxResponseModel
        //        {
        //            data = results.ToArray(),
        //            draw = Model.draw,
        //            recordsTotal = totalCount.ToString(),
        //            recordsFiltered = totalCount.ToString()
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error in GetIndexData: {ex.Message}");
        //        return BadRequest(new { error = ex.Message });
        //    }
        //}

        // Optimized TRC Report method with one month limit
        private async Task<TRCSessionModel> GetRevampedTRCReportData(DateTime From)
        {
            try
            {
                DateTime FromUTC = DateTime.SpecifyKind(From, DateTimeKind.Utc);

                // PERFORMANCE: Ensure we don't go beyond one month
                DateTime oneMonthAgo = DateTime.Now.AddDays(-30);
                if (FromUTC < oneMonthAgo)
                {
                    FromUTC = oneMonthAgo;
                }

                // PERFORMANCE OPTIMIZED: Limited to recent data with proper date filtering
                List<TRCReportGetAllDataQueryModel> QueryResult = await WidContext.Database.SqlQuery<TRCReportGetAllDataQueryModel>($$$"""
                    SELECT 
                        s.id, s.uid, 
                        s."ocrData" ->> 'userGivenNames' as "JORDANIAN_NAME",
                        s."ocrData" ->> 'userPersonalNumber' as "JORDANIAN_NATIONAL_ID",
                        s."ocrData" ->> 'nationality' as "NON_JORDANIAN_NATIONALITY",
                        s."ocrData" ->> 'documentNumber' as "DOCUMENT_NUMBER",
                        s."ocrData" ->> 'givenNames' as "NON_JORDANIAN_FIRST_NAME",
                        s."ocrData" ->> 'surname' as "NON_JORDANIAN_SURNAME",
                        s."ocrData" ->> 'userDateOfBirth' as "JORDANIAN_DOB",
                        s."ocrData" ->> 'dateOfBirth' as "NON_JORDANIAN_DOB",
                        s."creationTime" as "CREATION_TIME",
                        s."status" as "STATUS",
                        l."kitcode" as "KIT_CODE",
                        l."passportBarcode" as "PASSPORT_BARCODE", 
                        l."simCard" as "SIM_CARD",
                        l."nationalId" as "LINE_NATIONAL_ID",
                        l."flow" as "FLOW",
                        l."msisdn" as "MSISDN",
                        l."marketType" as "MARKET_TYPE",
                        sss.uid as "LATEST_SESSION_STEP_UID",
                        T."userCreationEmail" as "CHANNEL_USER"
                    FROM zain."Session" s
                    INNER JOIN "zain-custom"."Line" l ON s.uid = l.uid
                    INNER JOIN zain."Token" T ON T.id = s."tokenId"
                    LEFT JOIN (
                        SELECT DISTINCT ON (sw."sessionId") ss.uid, sw."sessionId"
                        FROM zain."SessionStep" ss
                        INNER JOIN zain."SessionWorkflow" sw ON sw.id = ss."sessionWorkflowId"
                        WHERE ss.active = true
                        ORDER BY sw."sessionId", ss.id DESC
                    ) sss ON sss."sessionId" = s.id
                    WHERE s."creationTime" >= {{{FromUTC}}} 
                    AND (
                        (s."ocrData" IS NOT NULL AND jsonb_typeof(s."ocrData") = 'object' AND s."ocrData" != '{}'::jsonb) 
                        OR l."flow" = 'SANAD'
                    )
                    ORDER BY s.id DESC
                    LIMIT 10000
                    """).ToListAsync();

                var model = new TRCSessionModel { Sessions = new List<TRCSession>() };

                // Process results in parallel for better performance
                var sessions = QueryResult.AsParallel().Select(session =>
                {
                    if (session.FLOW == "SANAD")
                    {
                        return CreateSanadSession(session);
                    }
                    else if (!string.IsNullOrEmpty(session.JORDANIAN_NATIONAL_ID))
                    {
                        return CreateJordanianSession(session);
                    }
                    else
                    {
                        return CreateNonJordanianSession(session);
                    }
                }).ToList();

                model.Sessions = sessions;

                // Calculate statistics efficiently
                var completedSessions = sessions.Where(x => x.Status == "Completed").ToList();
                var failedSessions = sessions.Where(x => x.Status == "Failed").ToList();

                model.NumberOfActivatedLines = completedSessions.Count;
                model.NumberOfFailedActivations = failedSessions.Count;
                model.NumberOfSanadActivatedLines = completedSessions.Count(x => x.ActivationType == "Sanad");
                model.NumberOfFailedActivationsPerSession = failedSessions.Count;
                model.NumberOfFailedActivationsPerNationalNumberOrDocumentNumber =
                    failedSessions.GroupBy(x => x.NationalID).Count();

                return model;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetRevampedTRCReportData: {ex.Message}");
                return new TRCSessionModel { Sessions = new List<TRCSession>() };
            }
        }

        // Helper methods for creating different session types (unchanged)
        private TRCSession CreateSanadSession(TRCReportGetAllDataQueryModel session)
        {
            var status = (session.STATUS == "approved" || session.STATUS == "approval_pending") ? "Completed" : "Failed";

            return new TRCSession
            {
                ActivationType = "Sanad",
                NationalID = session.LINE_NATIONAL_ID,
                DateOfBirth = "NA",
                FullName = "NA",
                Simcard = session.SIM_CARD,
                ActivationDate = session.CREATION_TIME.AddHours(3).ToString("f"),
                MSISDN = session.MSISDN,
                status = session.STATUS,
                SessionID = session.uid,
                IsJordanian = true,
                PersonalNumber = "NA",
                SelectedPackage = "NA",
                CurrentStep = session.LATEST_SESSION_STEP_UID ?? "NA",
                ActivationDateTicks = session.CREATION_TIME.Ticks,
                Status = status,
                Nationality = "Jordanian",
                Channel = GetChannelName(session.CHANNEL_USER)
            };
        }

        private TRCSession CreateJordanianSession(TRCReportGetAllDataQueryModel session)
        {
            var status = (session.STATUS == "approved" || session.STATUS == "approval_pending") ? "Completed" : "Failed";

            return new TRCSession
            {
                ActivationType = "eKYC",
                NationalID = session.JORDANIAN_NATIONAL_ID,
                DateOfBirth = session.JORDANIAN_DOB?.Split("T")[0] ?? "",
                FullName = session.JORDANIAN_NAME,
                Simcard = session.SIM_CARD,
                ActivationDate = session.CREATION_TIME.AddHours(3).ToString("f"),
                MSISDN = session.MSISDN,
                status = session.STATUS,
                SessionID = session.uid,
                IsJordanian = true,
                PersonalNumber = "NA",
                SelectedPackage = "NA",
                CurrentStep = session.LATEST_SESSION_STEP_UID ?? "NA",
                ActivationDateTicks = session.CREATION_TIME.Ticks,
                Status = status,
                Nationality = "JOR",
                Channel = GetChannelName(session.CHANNEL_USER)
            };
        }

        private TRCSession CreateNonJordanianSession(TRCReportGetAllDataQueryModel session)
        {
            var status = (session.STATUS == "approved" || session.STATUS == "approval_pending") ? "Completed" : "Failed";

            var dob = "";
            if (!string.IsNullOrEmpty(session.NON_JORDANIAN_DOB))
            {
                try
                {
                    var dobObj = JsonConvert.DeserializeObject<OcrData.DateOfBirth>(session.NON_JORDANIAN_DOB);
                    if (dobObj != null)
                    {
                        var year = (int)dobObj.year.Value;
                        year = year > 24 ? year + 1900 : year + 2000;
                        var dateOfBirth = new DateTime(year, (int)dobObj.month, (int)dobObj.day);
                        dob = dateOfBirth.ToString("yyyy-MM-dd");
                    }
                }
                catch
                {
                    dob = "";
                }
            }

            return new TRCSession
            {
                ActivationType = "eKYC",
                NationalID = session.DOCUMENT_NUMBER,
                DateOfBirth = dob,
                FullName = $"{session.NON_JORDANIAN_FIRST_NAME}{session.NON_JORDANIAN_SURNAME}",
                Simcard = session.SIM_CARD,
                ActivationDate = session.CREATION_TIME.AddHours(3).ToString("f"),
                MSISDN = session.MSISDN,
                status = session.STATUS,
                SessionID = session.uid,
                IsJordanian = false,
                PersonalNumber = session.PASSPORT_BARCODE,
                SelectedPackage = "NA",
                CurrentStep = session.LATEST_SESSION_STEP_UID,
                ActivationDateTicks = session.CREATION_TIME.Ticks,
                Status = status,
                Nationality = session.NON_JORDANIAN_NATIONALITY,
                Channel = GetChannelName(session.CHANNEL_USER)
            };
        }

        private string GetChannelName(string channelUser)
        {
            return channelUser switch
            {
                "consumer-shop-crm@079.jo" => "Shops Process",
                "consumer-shop@079.jo" => "eShop",
                "consumer@jo.zain.com" => "API Layer",
                _ => "079.jo"
            };
        }

        public async Task<IActionResult> Index()
        {
            return RedirectToAction(nameof(OptIndex));
        }

        public async Task<IActionResult> OptIndex()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> TRCReport()
        {
            return View(await GetTRCReportData(false));
        }

        [HttpGet]
        public async Task<IActionResult> DailyReport()
        {
            return View("TRCReport", await GetTRCReportData(true));
        }

        [HttpGet]
        public async Task<IActionResult> MonthlyReport()
        {
            // Limited to last 30 days for performance
            return View("TRCReport", await GetRevampedTRCReportData(DateTime.Now.AddDays(-30)));
        }

        [HttpGet]
        public async Task<IActionResult> SummaryDashboard()
        {
            return View("EnhancedSummary");
        }

        private async Task<TRCSessionModel> GetTRCReportData(bool isToday)
        {
            DateTime timespan = isToday ? DateTime.Today : DateTime.Now.AddDays(-30); // Changed from Dec 2024 to last 30 days
            return await GetRevampedTRCReportData(timespan);
        }
        //[HttpPost]
        //public async Task<IActionResult> GetAdvancedSummaryData([FromBody] SummaryFilterRequest request)
        //{
        //    try
        //    {
        //        DateTime fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-30);
        //        DateTime toDate = request.ToDate ?? DateTime.UtcNow;

        //        // Convert to unspecified kind for PostgreSQL timestamp without time zone
        //        fromDate = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified);
        //        toDate = DateTime.SpecifyKind(toDate.AddDays(1), DateTimeKind.Unspecified); // Include full day

        //        // Limit to 3 months for performance
        //        if ((toDate - fromDate).TotalDays > 90)
        //        {
        //            fromDate = toDate.AddDays(-90);
        //        }

        //        // Build WHERE clause with named parameters
        //        var whereConditions = new List<string>
        //{
        //    @"s.""creationTime"" >= @fromDate",
        //    @"s.""creationTime"" <= @toDate"
        //};

        //        var parameters = new List<object>
        //{
        //    new NpgsqlParameter("@fromDate", NpgsqlDbType.Timestamp) { Value = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified) },
        //    new NpgsqlParameter("@toDate", NpgsqlDbType.Timestamp) { Value = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified) }
        //};

        //        if (!string.IsNullOrEmpty(request.Status))
        //        {
        //            whereConditions.Add(@"s.""status"" = @status");
        //            parameters.Add(new NpgsqlParameter("@status", NpgsqlDbType.Text) { Value = request.Status });
        //        }

        //        if (!string.IsNullOrEmpty(request.Flow))
        //        {
        //            whereConditions.Add(@"l.""flow""::text = @flow");
        //            parameters.Add(new NpgsqlParameter("@flow", NpgsqlDbType.Text) { Value = request.Flow });
        //        }

        //        if (!string.IsNullOrEmpty(request.Channel))
        //        {
        //            whereConditions.Add(@"T.""userCreationEmail"" = @channel");
        //            parameters.Add(new NpgsqlParameter("@channel", NpgsqlDbType.Text) { Value = request.Channel });
        //        }

        //        string whereClause = string.Join(" AND ", whereConditions);

        //        var sql = $@"
        //WITH filtered_sessions AS (
        //    SELECT 
        //        s.id, s.uid, s.status, s.""creationTime"",
        //        s.""ocrData"",
        //        l.flow::text as flow,
        //        T.""userCreationEmail"" as channel,
        //        EXTRACT(HOUR FROM s.""creationTime"" AT TIME ZONE 'UTC' AT TIME ZONE '+03:00') as hour_of_day,
        //        EXTRACT(DOW FROM s.""creationTime"" AT TIME ZONE 'UTC' AT TIME ZONE '+03:00') as day_of_week,
        //        DATE(s.""creationTime"" AT TIME ZONE 'UTC' AT TIME ZONE '+03:00') as session_date,
        //        CASE WHEN s.""ocrData"" ? 'code' THEN 'non_jordanian' ELSE 'jordanian' END as nationality_type
        //    FROM zain.""Session"" s
        //    LEFT JOIN ""zain-custom"".""Line"" l ON s.uid = l.uid
        //    INNER JOIN zain.""Token"" T ON T.id = s.""tokenId""
        //    WHERE {whereClause}
        //),
        //workflow_data AS (
        //    SELECT DISTINCT ON (sw.""sessionId"")
        //        sw.""sessionId"",
        //        ss.uid as step_uid,
        //        ss.""index"" + 1 as step_number,
        //        CASE 
        //            WHEN ss.""index"" + 1 = 1 THEN 'scan_barcode'
        //            WHEN ss.""index"" + 1 = 2 THEN 'sim_selection'
        //            WHEN ss.""index"" + 1 = 3 THEN 'policy'
        //            WHEN ss.""index"" + 1 = 4 THEN 'document'
        //            WHEN ss.""index"" + 1 = 5 THEN 'ocr_summary'
        //            WHEN ss.""index"" + 1 = 6 THEN 'self_recording'
        //            WHEN ss.""index"" + 1 = 7 THEN 'identification_completed'
        //            WHEN ss.""index"" + 1 = 8 THEN 'package_selection'
        //            WHEN ss.""index"" + 1 = 9 THEN 'payment'
        //            ELSE 'unknown'
        //        END AS step_name
        //    FROM zain.""SessionWorkflow"" sw
        //    INNER JOIN zain.""SessionStep"" ss ON sw.id = ss.""sessionWorkflowId""
        //    WHERE ss.active = true
        //    ORDER BY sw.""sessionId"", ss.""index"" DESC
        //)
        //SELECT json_build_object(
        //    'total_sessions', (SELECT COUNT(*) FROM filtered_sessions),
        //    'status_breakdown', (
        //        SELECT json_agg(json_build_object('status', status, 'count', count, 'percentage', round((count::decimal / total_count * 100), 2)))
        //        FROM (
        //            SELECT status, COUNT(*) as count, 
        //                   (SELECT COUNT(*) FROM filtered_sessions) as total_count
        //            FROM filtered_sessions 
        //            GROUP BY status
        //        ) status_stats
        //    ),
        //    'hourly_distribution', (
        //        SELECT json_agg(json_build_object('hour', hour_of_day, 'count', count))
        //        FROM (
        //            SELECT hour_of_day, COUNT(*) as count
        //            FROM filtered_sessions
        //            GROUP BY hour_of_day
        //            ORDER BY hour_of_day
        //        ) hourly_stats
        //    ),
        //    'daily_distribution', (
        //        SELECT json_agg(json_build_object('day', day_name, 'count', count))
        //        FROM (
        //            SELECT 
        //                CASE day_of_week
        //                    WHEN 0 THEN 'Sunday'
        //                    WHEN 1 THEN 'Monday'
        //                    WHEN 2 THEN 'Tuesday'
        //                    WHEN 3 THEN 'Wednesday'
        //                    WHEN 4 THEN 'Thursday'
        //                    WHEN 5 THEN 'Friday'
        //                    WHEN 6 THEN 'Saturday'
        //                END as day_name,
        //                COUNT(*) as count
        //            FROM filtered_sessions
        //            GROUP BY day_of_week
        //            ORDER BY day_of_week
        //        ) daily_stats
        //    ),
        //    'time_series', (
        //        SELECT json_agg(json_build_object(
        //            'date', session_date,
        //            'total', total_count,
        //            'approved', approved_count,
        //            'pending', pending_count,
        //            'rejected', rejected_count,
        //            'working', working_count
        //        ) ORDER BY session_date)
        //        FROM (
        //            SELECT 
        //                session_date,
        //                COUNT(*) as total_count,
        //                COUNT(*) FILTER (WHERE status = 'approved') as approved_count,
        //                COUNT(*) FILTER (WHERE status = 'approval_pending') as pending_count,
        //                COUNT(*) FILTER (WHERE status = 'to_discard') as rejected_count,
        //                COUNT(*) FILTER (WHERE status = 'working') as working_count
        //            FROM filtered_sessions
        //            GROUP BY session_date
        //            ORDER BY session_date
        //        ) time_series_data
        //    ),
        //    'flow_distribution', (
        //        SELECT json_agg(json_build_object('flow', COALESCE(flow, 'Integration'), 'count', count))
        //        FROM (
        //            SELECT flow, COUNT(*) as count
        //            FROM filtered_sessions
        //            GROUP BY flow
        //        ) flow_stats
        //    ),
        //    'channel_distribution', (
        //        SELECT json_agg(json_build_object('channel', channel_name, 'count', count))
        //        FROM (
        //            SELECT 
        //                CASE channel
        //                    WHEN 'consumer-shop-crm@079.jo' THEN 'Shops Process'
        //                    WHEN 'consumer-shop@079.jo' THEN 'eShop'
        //                    WHEN 'consumer@jo.zain.com' THEN 'API Layer'
        //                    ELSE '079.jo'
        //                END as channel_name,
        //                COUNT(*) as count
        //            FROM filtered_sessions
        //            GROUP BY channel
        //        ) channel_stats
        //    ),
        //    'nationality_distribution', (
        //        SELECT json_agg(json_build_object('nationality', nationality_label, 'count', count))
        //        FROM (
        //            SELECT 
        //                CASE nationality_type
        //                    WHEN 'jordanian' THEN 'Jordanian'
        //                    WHEN 'non_jordanian' THEN 'Non-Jordanian'
        //                    ELSE 'Unknown'
        //                END as nationality_label,
        //                COUNT(*) as count
        //            FROM filtered_sessions
        //            GROUP BY nationality_type
        //        ) nationality_stats
        //    ),
        //    'step_completion', (
        //        SELECT json_agg(json_build_object('step', step_name, 'count', count))
        //        FROM (
        //            SELECT wd.step_name, COUNT(*) as count
        //            FROM filtered_sessions fs
        //            INNER JOIN workflow_data wd ON fs.id = wd.""sessionId""
        //            GROUP BY wd.step_name
        //            ORDER BY AVG(wd.step_number)
        //        ) step_stats
        //    ),
        //    'peak_hours', (
        //        SELECT json_agg(json_build_object('hour', hour_of_day, 'count', count, 'rank', rank))
        //        FROM (
        //            SELECT hour_of_day, COUNT(*) as count,
        //                   ROW_NUMBER() OVER (ORDER BY COUNT(*) DESC) as rank
        //            FROM filtered_sessions
        //            GROUP BY hour_of_day
        //            ORDER BY count DESC
        //            LIMIT 5
        //        ) peak_hours_data
        //    ),
        //    'success_rate', (
        //        SELECT json_build_object(
        //            'total_processed', COUNT(*),
        //            'successful', COUNT(*) FILTER (WHERE status IN ('approved', 'approval_pending')),
        //            'failed', COUNT(*) FILTER (WHERE status = 'to_discard'),
        //            'success_percentage', round((COUNT(*) FILTER (WHERE status IN ('approved', 'approval_pending'))::decimal / COUNT(*) * 100), 2)
        //        )
        //        FROM filtered_sessions
        //        WHERE status != 'working'
        //    )
        //) as summary_data";

        //        var summaryResult = await WidContext.Database
        //            .SqlQueryRaw<AdvancedSummaryResult>(sql, parameters.ToArray())
        //            .FirstOrDefaultAsync();

        //        return Ok(new
        //        {
        //            Status = 0,
        //            Data = JsonConvert.DeserializeObject(summaryResult.summary_data)
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error in GetAdvancedSummaryData: {ex.Message}");
        //        return Ok(new { Status = -1, Message = ex.Message });
        //    }
        //}

        //[HttpPost]
        //public async Task<IActionResult> GetAdvancedSummaryData([FromBody] SummaryFilterRequest request)
        //{
        //    try
        //    {
        //        DateTime fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-30);
        //        DateTime toDate = request.ToDate ?? DateTime.UtcNow;

        //        // Convert to unspecified kind for PostgreSQL timestamp without time zone
        //        fromDate = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified);
        //        toDate = DateTime.SpecifyKind(toDate.AddDays(1), DateTimeKind.Unspecified); // Include full day

        //        // Limit to 3 months for performance
        //        if ((toDate - fromDate).TotalDays > 90)
        //        {
        //            fromDate = toDate.AddDays(-90);
        //        }

        //        // Build WHERE clause with named parameters
        //        var whereConditions = new List<string>
        //{
        //    @"s.""creationTime"" >= @fromDate",
        //    @"s.""creationTime"" <= @toDate"
        //};

        //        var parameters = new List<object>
        //{
        //    new NpgsqlParameter("@fromDate", NpgsqlDbType.Timestamp) { Value = fromDate },
        //    new NpgsqlParameter("@toDate", NpgsqlDbType.Timestamp) { Value = toDate }
        //};

        //        if (!string.IsNullOrEmpty(request.Status))
        //        {
        //            whereConditions.Add(@"s.""status"" = @status");
        //            parameters.Add(new NpgsqlParameter("@status", NpgsqlDbType.Text) { Value = request.Status });
        //        }

        //        if (!string.IsNullOrEmpty(request.Flow))
        //        {
        //            whereConditions.Add(@"l.""flow""::text = @flow");
        //            parameters.Add(new NpgsqlParameter("@flow", NpgsqlDbType.Text) { Value = request.Flow });
        //        }

        //        if (!string.IsNullOrEmpty(request.Channel))
        //        {
        //            whereConditions.Add(@"T.""userCreationEmail"" = @channel");
        //            parameters.Add(new NpgsqlParameter("@channel", NpgsqlDbType.Text) { Value = request.Channel });
        //        }

        //        string whereClause = string.Join(" AND ", whereConditions);

        //        var sql = $@"
        //WITH filtered_sessions AS (
        //    SELECT 
        //        s.id, s.uid, s.status, s.""creationTime"",
        //        s.""ocrData"",
        //        l.flow::text as flow,
        //        T.""userCreationEmail"" as channel,
        //        EXTRACT(HOUR FROM s.""creationTime"" AT TIME ZONE 'UTC' AT TIME ZONE '+03:00') as hour_of_day,
        //        EXTRACT(DOW FROM s.""creationTime"" AT TIME ZONE 'UTC' AT TIME ZONE '+03:00') as day_of_week,
        //        DATE(s.""creationTime"" AT TIME ZONE 'UTC' AT TIME ZONE '+03:00') as session_date,
        //        CASE WHEN s.""ocrData"" ? 'code' THEN 'non_jordanian' ELSE 'jordanian' END as nationality_type
        //    FROM zain.""Session"" s
        //    LEFT JOIN ""zain-custom"".""Line"" l ON s.uid = l.uid
        //    INNER JOIN zain.""Token"" T ON T.id = s.""tokenId""
        //    WHERE {whereClause}
        //),
        //workflow_data AS (
        //    SELECT DISTINCT ON (sw.""sessionId"")
        //        sw.""sessionId"",
        //        ss.uid as step_uid,
        //        ss.""index"" + 1 as step_number,
        //        CASE 
        //            WHEN ss.""index"" + 1 = 1 THEN 'scan_barcode'
        //            WHEN ss.""index"" + 1 = 2 THEN 'sim_selection'
        //            WHEN ss.""index"" + 1 = 3 THEN 'policy'
        //            WHEN ss.""index"" + 1 = 4 THEN 'document'
        //            WHEN ss.""index"" + 1 = 5 THEN 'ocr_summary'
        //            WHEN ss.""index"" + 1 = 6 THEN 'self_recording'
        //            WHEN ss.""index"" + 1 = 7 THEN 'identification_completed'
        //            WHEN ss.""index"" + 1 = 8 THEN 'package_selection'
        //            WHEN ss.""index"" + 1 = 9 THEN 'payment'
        //            ELSE 'unknown'
        //        END AS step_name
        //    FROM zain.""SessionWorkflow"" sw
        //    INNER JOIN zain.""SessionStep"" ss ON sw.id = ss.""sessionWorkflowId""
        //    WHERE ss.active = true
        //    ORDER BY sw.""sessionId"", ss.""index"" DESC
        //)
        //SELECT json_build_object(
        //    'total_sessions', (SELECT COUNT(*) FROM filtered_sessions),
        //    'status_breakdown', (
        //        SELECT json_agg(json_build_object('status', status, 'count', count, 'percentage', round((count::decimal / total_count * 100), 2)))
        //        FROM (
        //            SELECT status, COUNT(*) as count, 
        //                   (SELECT COUNT(*) FROM filtered_sessions) as total_count
        //            FROM filtered_sessions 
        //            GROUP BY status
        //        ) status_stats
        //    ),
        //    'hourly_distribution', (
        //        SELECT json_agg(json_build_object('hour', hour_of_day, 'count', count))
        //        FROM (
        //            SELECT hour_of_day, COUNT(*) as count
        //            FROM filtered_sessions
        //            GROUP BY hour_of_day
        //            ORDER BY hour_of_day
        //        ) hourly_stats
        //    ),
        //    'daily_distribution', (
        //        SELECT json_agg(json_build_object('day', day_name, 'count', count))
        //        FROM (
        //            SELECT 
        //                CASE day_of_week
        //                    WHEN 0 THEN 'Sunday'
        //                    WHEN 1 THEN 'Monday'
        //                    WHEN 2 THEN 'Tuesday'
        //                    WHEN 3 THEN 'Wednesday'
        //                    WHEN 4 THEN 'Thursday'
        //                    WHEN 5 THEN 'Friday'
        //                    WHEN 6 THEN 'Saturday'
        //                END as day_name,
        //                COUNT(*) as count
        //            FROM filtered_sessions
        //            GROUP BY day_of_week
        //            ORDER BY day_of_week
        //        ) daily_stats
        //    ),
        //    'time_series', (
        //        SELECT json_agg(json_build_object(
        //            'date', session_date,
        //            'total', total_count,
        //            'approved', approved_count,
        //            'pending', pending_count,
        //            'rejected', rejected_count,
        //            'working', working_count
        //        ) ORDER BY session_date)
        //        FROM (
        //            SELECT 
        //                session_date,
        //                COUNT(*) as total_count,
        //                COUNT(*) FILTER (WHERE status = 'approved') as approved_count,
        //                COUNT(*) FILTER (WHERE status = 'approval_pending') as pending_count,
        //                COUNT(*) FILTER (WHERE status = 'to_discard') as rejected_count,
        //                COUNT(*) FILTER (WHERE status = 'working') as working_count
        //            FROM filtered_sessions
        //            GROUP BY session_date
        //            ORDER BY session_date
        //        ) time_series_data
        //    ),
        //    'flow_distribution', (
        //        SELECT json_agg(json_build_object('flow', COALESCE(flow, 'Integration'), 'count', count))
        //        FROM (
        //            SELECT flow, COUNT(*) as count
        //            FROM filtered_sessions
        //            GROUP BY flow
        //        ) flow_stats
        //    ),
        //    'channel_distribution', (
        //        SELECT json_agg(json_build_object('channel', channel_name, 'count', count))
        //        FROM (
        //            SELECT 
        //                CASE channel
        //                    WHEN 'consumer-shop-crm@079.jo' THEN 'Shops Process'
        //                    WHEN 'consumer-shop@079.jo' THEN 'eShop'
        //                    WHEN 'consumer@jo.zain.com' THEN 'API Layer'
        //                    ELSE '079.jo'
        //                END as channel_name,
        //                COUNT(*) as count
        //            FROM filtered_sessions
        //            GROUP BY channel
        //        ) channel_stats
        //    ),
        //    'nationality_distribution', (
        //        SELECT json_agg(json_build_object('nationality', nationality_label, 'count', count))
        //        FROM (
        //            SELECT 
        //                CASE nationality_type
        //                    WHEN 'jordanian' THEN 'Jordanian'
        //                    WHEN 'non_jordanian' THEN 'Non-Jordanian'
        //                    ELSE 'Unknown'
        //                END as nationality_label,
        //                COUNT(*) as count
        //            FROM filtered_sessions
        //            GROUP BY nationality_type
        //        ) nationality_stats
        //    ),
        //    'step_completion', (
        //        SELECT json_agg(json_build_object('step', step_name, 'count', count))
        //        FROM (
        //            SELECT wd.step_name, COUNT(*) as count
        //            FROM filtered_sessions fs
        //            INNER JOIN workflow_data wd ON fs.id = wd.""sessionId""
        //            GROUP BY wd.step_name
        //            ORDER BY AVG(wd.step_number)
        //        ) step_stats
        //    ),
        //    'peak_hours', (
        //        SELECT json_agg(json_build_object('hour', hour_of_day, 'count', count, 'rank', rank))
        //        FROM (
        //            SELECT hour_of_day, COUNT(*) as count,
        //                   ROW_NUMBER() OVER (ORDER BY COUNT(*) DESC) as rank
        //            FROM filtered_sessions
        //            GROUP BY hour_of_day
        //            ORDER BY count DESC
        //            LIMIT 5
        //        ) peak_hours_data
        //    ),
        //    'success_rate', (
        //        SELECT json_build_object(
        //            'total_processed', COUNT(*),
        //            'successful', COUNT(*) FILTER (WHERE status IN ('approved', 'approval_pending')),
        //            'failed', COUNT(*) FILTER (WHERE status = 'to_discard'),
        //            'success_percentage', round((COUNT(*) FILTER (WHERE status IN ('approved', 'approval_pending'))::decimal / COUNT(*) * 100), 2)
        //        )
        //        FROM filtered_sessions
        //        WHERE status != 'working'
        //    )
        //)::text as result";

        //        // Execute the query and get the JSON string directly
        //        using var connection = WidContext.Database.GetDbConnection();
        //        await connection.OpenAsync();

        //        using var command = connection.CreateCommand();
        //        command.CommandText = sql;
        //        command.Parameters.AddRange(parameters.ToArray());

        //        var jsonResult = await command.ExecuteScalarAsync() as string;

        //        if (string.IsNullOrEmpty(jsonResult))
        //        {
        //            return Ok(new { Status = -1, Message = "No data returned from query" });
        //        }

        //        // Parse the JSON string to object
        //        var data = JsonConvert.DeserializeObject(jsonResult);

        //        return Ok(new
        //        {
        //            Status = 0,
        //            Data = data
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error in GetAdvancedSummaryData: {ex.Message}");
        //        return Ok(new { Status = -1, Message = ex.Message });
        //    }
        //}
        //[HttpGet]
        //public async Task<IActionResult> GetFilterOptions()
        //{
        //    try
        //    {
        //        var filterOptions = await WidContext.Database.SqlQuery<FilterOptionsResult>($$$"""
        //    WITH recent_data AS (
        //        SELECT DISTINCT
        //            s.status,
        //            l.flow,
        //            T."userCreationEmail" as channel
        //        FROM zain."Session" s
        //        LEFT JOIN "zain-custom"."Line" l ON s.uid = l.uid
        //        INNER JOIN zain."Token" T ON T.id = s."tokenId"
        //        WHERE s."creationTime" >= NOW() - INTERVAL '6 months'
        //    )
        //    SELECT json_build_object(
        //        'statuses', (
        //            SELECT json_agg(DISTINCT status ORDER BY status)
        //            FROM recent_data
        //            WHERE status IS NOT NULL
        //        ),
        //        'flows', (
        //            SELECT json_agg(DISTINCT flow ORDER BY flow)
        //            FROM recent_data
        //            WHERE flow IS NOT NULL
        //        ),
        //        'channels', (
        //            SELECT json_agg(json_build_object(
        //                'value', channel,
        //                'label', CASE channel
        //                    WHEN 'consumer-shop-crm@079.jo' THEN 'Shops Process'
        //                    WHEN 'consumer-shop@079.jo' THEN 'eShop'
        //                    WHEN 'consumer@jo.zain.com' THEN 'API Layer'
        //                    ELSE '079.jo'
        //                END
        //            ) ORDER BY channel)
        //            FROM (SELECT DISTINCT channel FROM recent_data WHERE channel IS NOT NULL) channels
        //        )
        //    ) as filter_options
        //    """).FirstOrDefaultAsync();

        //        return Ok(new
        //        {
        //            Status = 0,
        //            Data = JsonConvert.DeserializeObject(filterOptions.filter_options)
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error in GetFilterOptions: {ex.Message}");
        //        return Ok(new { Status = -1, Message = ex.Message });
        //    }
        //}
        [HttpPost]
        public async Task<IActionResult> GetAdvancedSummaryData([FromBody] SummaryFilterRequest request)
        {
            try
            {
                DateTime fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-30);
                DateTime toDate = request.ToDate ?? DateTime.UtcNow;

                // Convert to unspecified kind for PostgreSQL timestamp without time zone
                fromDate = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified);
                toDate = DateTime.SpecifyKind(toDate.AddDays(1), DateTimeKind.Unspecified); // Include full day

                // Limit to 3 months for performance
                if ((toDate - fromDate).TotalDays > 90)
                {
                    fromDate = toDate.AddDays(-90);
                }

                // Build WHERE clause with named parameters
                var whereConditions = new List<string>
        {
            @"s.""creationTime"" >= @fromDate",
            @"s.""creationTime"" <= @toDate"
        };

                var parameters = new List<object>
        {
            new NpgsqlParameter("@fromDate", NpgsqlDbType.Timestamp) { Value = fromDate },
            new NpgsqlParameter("@toDate", NpgsqlDbType.Timestamp) { Value = toDate }
        };

                if (!string.IsNullOrEmpty(request.Status))
                {
                    whereConditions.Add(@"s.""status"" = @status");
                    parameters.Add(new NpgsqlParameter("@status", NpgsqlDbType.Text) { Value = request.Status });
                }

                if (!string.IsNullOrEmpty(request.Flow))
                {
                    whereConditions.Add(@"l.""flow""::text = @flow");
                    parameters.Add(new NpgsqlParameter("@flow", NpgsqlDbType.Text) { Value = request.Flow });
                }

                if (!string.IsNullOrEmpty(request.Channel))
                {
                    whereConditions.Add(@"T.""userCreationEmail"" = @channel");
                    parameters.Add(new NpgsqlParameter("@channel", NpgsqlDbType.Text) { Value = request.Channel });
                }

                string whereClause = string.Join(" AND ", whereConditions);

                var sql = $@"
        WITH filtered_sessions AS (
            SELECT 
                s.id, s.uid, s.status, s.""creationTime"",
                s.""ocrData"",
                l.flow::text as flow,
                T.""userCreationEmail"" as channel,
                EXTRACT(HOUR FROM s.""creationTime"" AT TIME ZONE 'UTC' AT TIME ZONE '+03:00') as hour_of_day,
                EXTRACT(DOW FROM s.""creationTime"" AT TIME ZONE 'UTC' AT TIME ZONE '+03:00') as day_of_week,
                DATE(s.""creationTime"" AT TIME ZONE 'UTC' AT TIME ZONE '+03:00') as session_date,
                CASE WHEN s.""ocrData"" ? 'code' THEN 'non_jordanian' ELSE 'jordanian' END as nationality_type
            FROM zain.""Session"" s
            LEFT JOIN ""zain-custom"".""Line"" l ON s.uid = l.uid
            INNER JOIN zain.""Token"" T ON T.id = s.""tokenId""
            WHERE {whereClause}
        ),
        workflow_data AS (
            SELECT DISTINCT ON (sw.""sessionId"")
                sw.""sessionId"",
                ss.uid as step_uid,
                ss.""index"" + 1 as step_number,
                CASE 
                    WHEN ss.""index"" + 1 = 1 THEN 'scan_barcode'
                    WHEN ss.""index"" + 1 = 2 THEN 'sim_selection'
                    WHEN ss.""index"" + 1 = 3 THEN 'policy'
                    WHEN ss.""index"" + 1 = 4 THEN 'document'
                    WHEN ss.""index"" + 1 = 5 THEN 'ocr_summary'
                    WHEN ss.""index"" + 1 = 6 THEN 'self_recording'
                    WHEN ss.""index"" + 1 = 7 THEN 'identification_completed'
                    WHEN ss.""index"" + 1 = 8 THEN 'package_selection'
                    WHEN ss.""index"" + 1 = 9 THEN 'payment'
                    ELSE 'unknown'
                END AS step_name
            FROM zain.""SessionWorkflow"" sw
            INNER JOIN zain.""SessionStep"" ss ON sw.id = ss.""sessionWorkflowId""
            WHERE ss.active = true
            ORDER BY sw.""sessionId"", ss.""index"" DESC
        )
        SELECT json_build_object(
            'total_sessions', (SELECT COUNT(*) FROM filtered_sessions),
            'status_breakdown', (
                SELECT COALESCE(json_agg(json_build_object('status', status, 'count', count, 'percentage', round((count::decimal / total_count * 100), 2))), '[]'::json)
                FROM (
                    SELECT status, COUNT(*) as count, 
                           (SELECT COUNT(*) FROM filtered_sessions) as total_count
                    FROM filtered_sessions 
                    GROUP BY status
                ) status_stats
            ),
            'hourly_distribution', (
                SELECT COALESCE(json_agg(json_build_object('hour', hour_of_day, 'count', count)), '[]'::json)
                FROM (
                    SELECT hour_of_day, COUNT(*) as count
                    FROM filtered_sessions
                    GROUP BY hour_of_day
                    ORDER BY hour_of_day
                ) hourly_stats
            ),
            'daily_distribution', (
                SELECT COALESCE(json_agg(json_build_object('day', day_name, 'count', count)), '[]'::json)
                FROM (
                    SELECT 
                        CASE day_of_week
                            WHEN 0 THEN 'Sunday'
                            WHEN 1 THEN 'Monday'
                            WHEN 2 THEN 'Tuesday'
                            WHEN 3 THEN 'Wednesday'
                            WHEN 4 THEN 'Thursday'
                            WHEN 5 THEN 'Friday'
                            WHEN 6 THEN 'Saturday'
                        END as day_name,
                        COUNT(*) as count
                    FROM filtered_sessions
                    GROUP BY day_of_week
                    ORDER BY day_of_week
                ) daily_stats
            ),
            'time_series', (
                SELECT COALESCE(json_agg(json_build_object(
                    'date', session_date,
                    'total', total_count,
                    'approved', approved_count,
                    'pending', pending_count,
                    'rejected', rejected_count,
                    'working', working_count
                ) ORDER BY session_date), '[]'::json)
                FROM (
                    SELECT 
                        session_date,
                        COUNT(*) as total_count,
                        COUNT(*) FILTER (WHERE status = 'approved') as approved_count,
                        COUNT(*) FILTER (WHERE status = 'approval_pending') as pending_count,
                        COUNT(*) FILTER (WHERE status = 'to_discard') as rejected_count,
                        COUNT(*) FILTER (WHERE status = 'working') as working_count
                    FROM filtered_sessions
                    GROUP BY session_date
                    ORDER BY session_date
                ) time_series_data
            ),
            'flow_distribution', (
                SELECT COALESCE(json_agg(json_build_object('flow', COALESCE(flow, 'Integration'), 'count', count)), '[]'::json)
                FROM (
                    SELECT flow, COUNT(*) as count
                    FROM filtered_sessions
                    GROUP BY flow
                ) flow_stats
            ),
            'channel_distribution', (
                SELECT COALESCE(json_agg(json_build_object('channel', channel_name, 'count', count)), '[]'::json)
                FROM (
                    SELECT 
                        CASE channel
                            WHEN 'consumer-shop-crm@079.jo' THEN 'Shops Process'
                            WHEN 'consumer-shop@079.jo' THEN 'eShop'
                            WHEN 'consumer@jo.zain.com' THEN 'API Layer'
                            ELSE '079.jo'
                        END as channel_name,
                        COUNT(*) as count
                    FROM filtered_sessions
                    GROUP BY channel
                ) channel_stats
            ),
            'nationality_distribution', (
                SELECT COALESCE(json_agg(json_build_object('nationality', nationality_label, 'count', count)), '[]'::json)
                FROM (
                    SELECT 
                        CASE nationality_type
                            WHEN 'jordanian' THEN 'Jordanian'
                            WHEN 'non_jordanian' THEN 'Non-Jordanian'
                            ELSE 'Unknown'
                        END as nationality_label,
                        COUNT(*) as count
                    FROM filtered_sessions
                    GROUP BY nationality_type
                ) nationality_stats
            ),
            'step_completion', (
                SELECT COALESCE(json_agg(json_build_object('step', step_name, 'count', count)), '[]'::json)
                FROM (
                    SELECT wd.step_name, COUNT(*) as count
                    FROM filtered_sessions fs
                    INNER JOIN workflow_data wd ON fs.id = wd.""sessionId""
                    GROUP BY wd.step_name
                    ORDER BY AVG(wd.step_number)
                ) step_stats
            ),
            'peak_hours', (
                SELECT COALESCE(json_agg(json_build_object('hour', hour_of_day, 'count', count, 'rank', rank)), '[]'::json)
                FROM (
                    SELECT hour_of_day, COUNT(*) as count,
                           ROW_NUMBER() OVER (ORDER BY COUNT(*) DESC) as rank
                    FROM filtered_sessions
                    GROUP BY hour_of_day
                    ORDER BY count DESC
                    LIMIT 5
                ) peak_hours_data
            ),
            'success_rate', (
                SELECT json_build_object(
                    'total_processed', COALESCE(COUNT(*), 0),
                    'successful', COALESCE(COUNT(*) FILTER (WHERE status IN ('approved', 'approval_pending')), 0),
                    'failed', COALESCE(COUNT(*) FILTER (WHERE status = 'to_discard'), 0),
                    'success_percentage', CASE 
                        WHEN COUNT(*) > 0 THEN round((COUNT(*) FILTER (WHERE status IN ('approved', 'approval_pending'))::decimal / COUNT(*) * 100), 2)
                        ELSE 0
                    END
                )
                FROM filtered_sessions
                WHERE status != 'working'
            )
        )::text as result";

                // Execute the query and get the JSON string directly
                using var connection = WidContext.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddRange(parameters.ToArray());

                var jsonResult = await command.ExecuteScalarAsync() as string;

                if (string.IsNullOrEmpty(jsonResult))
                {
                    return Ok(new
                    {
                        Status = -1,
                        Message = "No data returned from query",
                        Data = new AdvancedSummaryResponse() // Return empty structure
                    });
                }

                try
                {
                    // Parse the JSON string properly using JObject first, then convert to strongly typed
                    var jsonObject = JObject.Parse(jsonResult);
                    var data = jsonObject.ToObject<AdvancedSummaryResponse>();

                    return Ok(new
                    {
                        Status = 0,
                        Data = data
                    });
                }
                catch (JsonException jsonEx)
                {
                    Console.WriteLine($"JSON parsing error: {jsonEx.Message}");
                    Console.WriteLine($"Raw JSON: {jsonResult}");

                    // Fallback: return raw parsed JSON
                    var fallbackData = JObject.Parse(jsonResult);
                    return Ok(new
                    {
                        Status = 0,
                        Data = fallbackData
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAdvancedSummaryData: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Ok(new
                {
                    Status = -1,
                    Message = ex.Message,
                    Data = new AdvancedSummaryResponse() // Return empty structure even on error
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFilterOptions()
        {
            try
            {
                var filterOptions = await WidContext.Database.SqlQuery<FilterOptionsResult>($$$"""
            WITH recent_data AS (
                SELECT DISTINCT
                    s.status,
                    l.flow,
                    T."userCreationEmail" as channel
                FROM zain."Session" s
                LEFT JOIN "zain-custom"."Line" l ON s.uid = l.uid
                INNER JOIN zain."Token" T ON T.id = s."tokenId"
                WHERE s."creationTime" >= NOW() - INTERVAL '6 months'
            )
            SELECT json_build_object(
                'statuses', (
                    SELECT COALESCE(json_agg(DISTINCT status ORDER BY status), '[]'::json)
                    FROM recent_data
                    WHERE status IS NOT NULL
                ),
                'flows', (
                    SELECT COALESCE(json_agg(DISTINCT flow ORDER BY flow), '[]'::json)
                    FROM recent_data
                    WHERE flow IS NOT NULL
                ),
                'channels', (
                    SELECT COALESCE(json_agg(json_build_object(
                        'value', channel,
                        'label', CASE channel
                            WHEN 'consumer-shop-crm@079.jo' THEN 'Shops Process'
                            WHEN 'consumer-shop@079.jo' THEN 'eShop'
                            WHEN 'consumer@jo.zain.com' THEN 'API Layer'
                            ELSE '079.jo'
                        END
                    ) ORDER BY channel), '[]'::json)
                    FROM (SELECT DISTINCT channel FROM recent_data WHERE channel IS NOT NULL) channels
                )
            ) as filter_options
            """).FirstOrDefaultAsync();

                if (filterOptions?.filter_options == null)
                {
                    return Ok(new
                    {
                        Status = -1,
                        Message = "No filter options available",
                        Data = new FilterOptionsData()
                    });
                }

                try
                {
                    var data = JsonConvert.DeserializeObject<FilterOptionsData>(filterOptions.filter_options);
                    return Ok(new
                    {
                        Status = 0,
                        Data = data
                    });
                }
                catch (JsonException jsonEx)
                {
                    Console.WriteLine($"JSON parsing error in GetFilterOptions: {jsonEx.Message}");

                    // Fallback: return raw parsed JSON
                    var fallbackData = JObject.Parse(filterOptions.filter_options);
                    return Ok(new
                    {
                        Status = 0,
                        Data = fallbackData
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetFilterOptions: {ex.Message}");
                return Ok(new
                {
                    Status = -1,
                    Message = ex.Message,
                    Data = new FilterOptionsData()
                });
            }
        }

        // Helper classes for the new methods
        public class SummaryFilterRequest
        {
            public DateTime? FromDate { get; set; }
            public DateTime? ToDate { get; set; }
            public string? Status { get; set; }
            public string? Flow { get; set; }
            public string? Channel { get; set; }
        }

        public class AdvancedSummaryResult
        {
            public string summary_data { get; set; }
        }

        public class FilterOptionsResult
        {
            public string filter_options { get; set; }
        }

        // API METHODS WITH PERFORMANCE LIMITS
        [HttpPost]
        public async Task<IActionResult> GetOCRFile([FromBody] GetDataRequest Request)
        {
            try
            {
                // FIXED: Convert to UTC to avoid PostgreSQL timezone error
                DateTime oneMonthAgoUtc = DateTime.SpecifyKind(DateTime.Now.AddDays(-30), DateTimeKind.Utc);

                var QueryResult = await WidContext.Database.SqlQuery<GenericDataQueryResult>($$"""
                SELECT "ocrData"::text as result
                FROM zain."Session"
                where uid = {{Request.uid}}
                AND "creationTime" >= {{oneMonthAgoUtc}}
                """).FirstOrDefaultAsync();

                return Ok(new GetDataResponse<string>
                {
                    Status = 0,
                    data = QueryResult?.result
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetOCRFile: {ex.Message}");
                return Ok(new GetDataResponse<string> { Status = -1, data = null });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetLivenessOutput([FromBody] GetDataRequest Request)
        {
            try
            {
                // FIXED: Convert to UTC to avoid PostgreSQL timezone error
                DateTime oneMonthAgoUtc = DateTime.SpecifyKind(DateTime.Now.AddDays(-30), DateTimeKind.Utc);

                var QueryResult = await WidContext.Database.SqlQuery<GenericDataQueryResult>($$"""
                SELECT "livenessOutput"::text as result
                FROM zain."Session"
                where uid = {{Request.uid}}
                AND "creationTime" >= {{oneMonthAgoUtc}}
                """).FirstOrDefaultAsync();

                return Ok(new GetDataResponse<string>
                {
                    Status = 0,
                    data = QueryResult?.result
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetLivenessOutput: {ex.Message}");
                return Ok(new GetDataResponse<string> { Status = -1, data = null });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetSessionSteps([FromBody] GetDataRequest Request)
        {
            try
            {
                // FIXED: Convert to UTC to avoid PostgreSQL timezone error
                DateTime oneMonthAgoUtc = DateTime.SpecifyKind(DateTime.Now.AddDays(-30), DateTimeKind.Utc);

                var SessionWorkflowResult = await WidContext.Database.SqlQuery<GenericDataQueryResult>($$"""
                SELECT sw.id::text as result
                FROM zain."SessionWorkflow" sw
                INNER JOIN zain."Session" s ON s.id = sw."sessionId"
                where sw."sessionId" = {{Request.id}}
                """).FirstOrDefaultAsync();

                if (SessionWorkflowResult?.result == null)
                {
                    return Ok(new GetDataResponse<string> { Status = 0, data = "[]" });
                }

                int SessionWorkflowID = int.Parse(SessionWorkflowResult.result);
                var QueryResult = await WidContext.Database.SqlQuery<SessionStepNoNavigations>($$"""
                SELECT *
                FROM zain."SessionStep"
                where "sessionWorkflowId" = {{SessionWorkflowID}}
                order by id asc
                """).ToListAsync();

                return Ok(new GetDataResponse<string>
                {
                    Status = 0,
                    data = JsonConvert.SerializeObject(QueryResult)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetSessionSteps: {ex.Message}");
                return Ok(new GetDataResponse<string> { Status = -1, data = null });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetAuditData([FromBody] GetDataRequest Request)
        {
            try
            {
                var QueryResult = await WidContext.Database.SqlQuery<GenericDataQueryResult>($$"""
                SELECT a.content
                FROM zain."Audit" a
                INNER JOIN zain."Session" s ON s.id = a."sessionId"
                where a."sessionId" = {{Request.id}}
                order by a.id asc
                """).ToListAsync();

                return Ok(new GetDataResponse<string>
                {
                    Status = 0,
                    data = JsonConvert.SerializeObject(QueryResult.Select(x => x.result))
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAuditData: {ex.Message}");
                return Ok(new GetDataResponse<string> { Status = -1, data = null });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetAllMedia([FromBody] GetDataRequest Request)
        {
            try
            {
                var QueryResult = await WidContext.Database.SqlQuery<Medium>($$"""
                SELECT m.*
                FROM zain."Media" m
                INNER JOIN zain."Session" s ON s."tokenId" = m."tokenId"
                where m."tokenId" = {{Request.tokenId}}
      
                """).ToListAsync();

                return Ok(new GetDataResponse<string>
                {
                    Status = 0,
                    data = JsonConvert.SerializeObject(QueryResult)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAllMedia: {ex.Message}");
                return Ok(new GetDataResponse<string> { Status = -1, data = null });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetMediaFile([FromBody] Medium Media)
        {
            try
            {
                if (Media == null)
                {
                    return BadRequest("Media object is required");
                }

                var token = await EkycClient.GenerateToken();
                if (token != null)
                {
                    var FileStream = await EkycClient.GetMediaFile(Media.Uid, token.access_token);
                    string FileExtension = Media.Filename.Split(".").Last();
                    string FileMimeType = StaticHelpers.GetMimeType(FileExtension);
                    return File(FileStream, FileMimeType, Media.Filename);
                }
                return BadRequest("Unable to generate token");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetMediaFile: {ex.Message}");
                return BadRequest("Error retrieving media file");
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetLineInformation([FromBody] GetDataRequest Request)
        {
            try
            {
                var uid = Request.uid;
                var utcThresholdDate = DateTime.UtcNow.AddDays(-30);

                var query = WidContext.Lines
                    .FromSqlInterpolated($@"
                SELECT l.*
                FROM ""zain-custom"".""Line"" l
                INNER JOIN zain.""Session"" s ON s.uid = l.uid
                WHERE l.uid = {uid}
                AND s.""creationTime"" >= {utcThresholdDate}
            ");

                var QueryResult = await query.FirstOrDefaultAsync();

                return Ok(new GetDataResponse<string>
                {
                    Status = 0,
                    data = JsonConvert.SerializeObject(QueryResult)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetLineInformation: {ex.Message}");
                return Ok(new GetDataResponse<string> { Status = -1, data = null });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetLivenessThreshold([FromBody] GetDataRequest Request)
        {
            try
            {
                var QueryResult = await WidContext.Database.SqlQuery<GenericDataQueryResult>($$"""
                SELECT "livenessThreshold"::text as result
                FROM zain."Session"
                where uid = {{{Request.uid}}}
                AND "creationTime" >= {{{DateTime.Now.AddDays(-30)}}}
                """).FirstOrDefaultAsync();

                return Ok(new GetDataResponse<string>
                {
                    Status = 0,
                    data = QueryResult?.result
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetLivenessThreshold: {ex.Message}");
                return Ok(new GetDataResponse<string> { Status = -1, data = null });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetFaceMatchThreshold([FromBody] GetDataRequest Request)
        {
            try
            {
                var QueryResult = await WidContext.Database.SqlQuery<GenericDataQueryResult>($$"""
                SELECT "faceMatchThreshold"::text as result
                FROM zain."Session"
                where uid = {{{Request.uid}}}
                AND "creationTime" >= {{{DateTime.Now.AddDays(-30)}}}
                """).FirstOrDefaultAsync();

                return Ok(new GetDataResponse<string>
                {
                    Status = 0,
                    data = QueryResult?.result
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetFaceMatchThreshold: {ex.Message}");
                return Ok(new GetDataResponse<string> { Status = -1, data = null });
            }
        }

        public async Task<IActionResult> SanadUsers()
        {
            try
            {
                // PERFORMANCE: Limited to last 30 days
                DateTime oneMonthAgoUtc = DateTime.SpecifyKind(DateTime.Now.AddDays(-30), DateTimeKind.Utc);

                // Optimized query to get Sanad users data in one go
                var QueryResult = await WidContext.Database.SqlQuery<SanadUserQueryModel>($$"""
                    SELECT 
                        s.id, s.uid, s."creationTime",
                        l."nationalId",
                        sss.uid as "LATEST_SESSION_STEP_UID"
                    FROM zain."Session" s
                    INNER JOIN "zain-custom"."Line" l ON s.uid = l.uid
                    LEFT JOIN (
                        SELECT DISTINCT ON (sw."sessionId") ss.uid, sw."sessionId"
                        FROM zain."SessionStep" ss
                        INNER JOIN zain."SessionWorkflow" sw ON sw.id = ss."sessionWorkflowId"
                        WHERE ss.active = true
                        ORDER BY sw."sessionId", ss.id DESC
                    ) sss ON sss."sessionId" = s.id
                    WHERE l.flow = 'SANAD'
                    AND s."creationTime" >= {{oneMonthAgoUtc}}
                    ORDER BY s.id DESC
                    LIMIT 5000
                    """).ToListAsync();

                var sessionIds = QueryResult.Select(x => x.id).ToList();
                var sessionUids = QueryResult.Select(x => x.uid).ToList();

                // Get audit data for these sessions
                var auditData = await WidContext.Audits
                    .Where(x => sessionIds.Contains(x.SessionId.GetValueOrDefault()))
                    .Select(x => new { x.SessionId, x.Content })
                    .AsNoTracking()
                    .ToListAsync();

                // Get session steps data
                var sessionWorkflows = await WidContext.SessionWorkflows
                    .Where(x => sessionIds.Contains(x.SessionId))
                    .AsNoTracking()
                    .ToListAsync();

                var workflowIds = sessionWorkflows.Select(x => x.Id).ToList();
                var sessionSteps = await WidContext.SessionSteps
                    .Where(x => workflowIds.Contains(x.SessionWorkflowId))
                    .OrderBy(x => x.Index)
                    .AsNoTracking()
                    .ToListAsync();

                // Get line data
                var lineData = await WidContext.Lines
                    .Where(x => sessionUids.Contains(x.Uid) && x.flow == "SANAD")
                    .AsNoTracking()
                    .ToListAsync();

                // Build the model
                var auditLookup = auditData.GroupBy(x => x.SessionId).ToDictionary(g => g.Key, g => g.Select(a => a.Content).ToList());
                var lineLookup = lineData.ToDictionary(x => x.Uid, x => x);
                var workflowLookup = sessionWorkflows.ToDictionary(x => x.SessionId, x => x);
                var stepLookup = sessionSteps
                    .GroupBy(x => x.SessionWorkflowId)
                    .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Index).ToList());

                // Build the model efficiently
                List<SessionModel> Model = QueryResult.Select(x =>
                {
                    workflowLookup.TryGetValue(x.id, out var workflow);
                    var steps = (workflow != null && stepLookup.TryGetValue(workflow.Id, out var workflowSteps)) ? workflowSteps : null;
                    auditLookup.TryGetValue(x.id, out var audits);
                    lineLookup.TryGetValue(x.uid, out var line);

                    return new SessionModel
                    {
                        Session = new Session { Id = x.id, Uid = x.uid, CreationTime = x.creationTime },
                        Audits = audits ?? new List<string>(),
                        Line = line,
                        SessionSteps = steps
                    };
                }).ToList();

                return View("SanadLog", Model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SanadUsers: {ex.Message}");
                return View("SanadLog", new List<SessionModel>());
            }
        }
    }

    // Add these helper classes for the optimized queries
    public class SummaryDataResult
    {
        public string sessions_count { get; set; }
        public string steps_count { get; set; }
        public string nationality_comparison { get; set; }
        public string time_series_report { get; set; }
    }

    public class FlowChannelResult
    {
        public string flow_count { get; set; }
        public string channel_comparisons { get; set; }
    }

    public class SanadUserQueryModel
    {
        public int id { get; set; }
        public string uid { get; set; }
        public DateTime creationTime { get; set; }
        public string nationalId { get; set; }
        public string? LATEST_SESSION_STEP_UID { get; set; }
    }

    public class GetDataRequest
    {
        public string uid { get; set; }
        public int id { get; set; }
        public int tokenId { get; set; }
    }

    public class GetDataResponse<T>
    {
        public int Status { get; set; }
        public T data { get; set; }
    }

    public class GenericDataQueryResult
    {
        public string result { get; set; }
    }

    // Add static helper class for MIME types
    public static class StaticHelpers
    {
        public static string GetMimeType(string fileExtension)
        {
            return fileExtension.ToLower() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                ".mp4" => "video/mp4",
                ".avi" => "video/avi",
                ".mov" => "video/mov",
                ".txt" => "text/plain",
                ".json" => "application/json",
                _ => "application/octet-stream"
            };
        }
    }
    public class AdvancedSummaryResponse
    {
        [JsonProperty("total_sessions")]
        public int TotalSessions { get; set; }

        [JsonProperty("status_breakdown")]
        public List<StatusBreakdownItem> StatusBreakdown { get; set; } = new();

        [JsonProperty("hourly_distribution")]
        public List<HourlyDistributionItem> HourlyDistribution { get; set; } = new();

        [JsonProperty("daily_distribution")]
        public List<DailyDistributionItem> DailyDistribution { get; set; } = new();

        [JsonProperty("time_series")]
        public List<TimeSeriesItem> TimeSeries { get; set; } = new();

        [JsonProperty("flow_distribution")]
        public List<FlowDistributionItem> FlowDistribution { get; set; } = new();

        [JsonProperty("channel_distribution")]
        public List<ChannelDistributionItem> ChannelDistribution { get; set; } = new();

        [JsonProperty("nationality_distribution")]
        public List<NationalityDistributionItem> NationalityDistribution { get; set; } = new();

        [JsonProperty("step_completion")]
        public List<StepCompletionItem> StepCompletion { get; set; } = new();

        [JsonProperty("peak_hours")]
        public List<PeakHourItem> PeakHours { get; set; } = new();

        [JsonProperty("success_rate")]
        public SuccessRateData SuccessRate { get; set; } = new();
    }
    public class StatusBreakdownItem
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("percentage")]
        public decimal Percentage { get; set; }
    }

    public class HourlyDistributionItem
    {
        [JsonProperty("hour")]
        public int Hour { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }

    public class DailyDistributionItem
    {
        [JsonProperty("day")]
        public string Day { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }

    public class TimeSeriesItem
    {
        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("approved")]
        public int Approved { get; set; }

        [JsonProperty("pending")]
        public int Pending { get; set; }

        [JsonProperty("rejected")]
        public int Rejected { get; set; }

        [JsonProperty("working")]
        public int Working { get; set; }
    }

    public class FlowDistributionItem
    {
        [JsonProperty("flow")]
        public string Flow { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }

    public class ChannelDistributionItem
    {
        [JsonProperty("channel")]
        public string Channel { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }

    public class NationalityDistributionItem
    {
        [JsonProperty("nationality")]
        public string Nationality { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }

    public class StepCompletionItem
    {
        [JsonProperty("step")]
        public string Step { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }

    public class PeakHourItem
    {
        [JsonProperty("hour")]
        public int Hour { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }
    }

    public class SuccessRateData
    {
        [JsonProperty("total_processed")]
        public int TotalProcessed { get; set; }

        [JsonProperty("successful")]
        public int Successful { get; set; }

        [JsonProperty("failed")]
        public int Failed { get; set; }

        [JsonProperty("success_percentage")]
        public decimal SuccessPercentage { get; set; }
    }

    public class SummaryFilterRequest
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Status { get; set; }
        public string Flow { get; set; }
        public string Channel { get; set; }
    }

    public class FilterOptionsResult
    {
        [JsonProperty("filter_options")]
        public string filter_options { get; set; }
    }

    public class FilterOptionsData
    {
        [JsonProperty("statuses")]
        public List<string> Statuses { get; set; } = new();

        [JsonProperty("flows")]
        public List<string> Flows { get; set; } = new();

        [JsonProperty("channels")]
        public List<ChannelOption> Channels { get; set; } = new();
    }

    public class ChannelOption
    {
        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }
    }
}