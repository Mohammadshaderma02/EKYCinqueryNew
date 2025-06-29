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
using Seq.Api;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
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

        // MAJOR OPTIMIZATION: Completely rewritten GetIndexData method with one month limit
        [HttpPost]
        public async Task<IActionResult> GetIndexData([FromForm] AjaxPostModel Model)
        {
            try
            {
                int PageNum = Model.start;
                int PageSize = Model.length;
                string search = Model?.search?.value?.Trim();

                DateTime oneMonthAgoUtc = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-30), DateTimeKind.Utc);

                using var connection = WidContext.Database.GetDbConnection();
                await connection.OpenAsync();

                var parameters = new List<(string name, object value)>
        {
            ("@oneMonthAgo", oneMonthAgoUtc),
            ("@PageSize", PageSize),
            ("@PageNum", PageNum)
        };

                var whereClauses = new List<string>
        {
            @"s.""ocrData"" IS NOT NULL",
            @"jsonb_typeof(s.""ocrData"") = 'object'",
            @"s.""ocrData"" != '{}'::jsonb",
            @"s.""creationTime"" >= @oneMonthAgo"
        };

                if (!string.IsNullOrEmpty(search))
                {
                    var searchParam = $"%{search}%";
                    parameters.Add(("@search", searchParam));

                    // Use individual JSONB fields (indexable) and avoid COALESCE
                    whereClauses.Add(@"
                (
                    s.""ocrData"" ->> 'userGivenNames' ILIKE @search OR
                    s.""ocrData"" ->> 'givenNames' ILIKE @search OR
                    s.""ocrData"" ->> 'userPersonalNumber' ILIKE @search OR
                    s.""ocrData"" ->> 'documentNumber' ILIKE @search OR
                    s.""ocrData"" ->> 'nationality' ILIKE @search OR
                    s.""uid""::text ILIKE @search OR
                    T.""userCreationEmail"" ILIKE @search
                )");
                }
                string whereClause = string.Join(" AND ", whereClauses);

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

                // Count query
                var countQuery = $"SELECT COUNT(*) FROM ({baseQuery}) AS count_alias";
                using var countCmd = connection.CreateCommand();
                countCmd.CommandText = countQuery;
                foreach (var param in parameters.Where(p => p.name != "@PageSize" && p.name != "@PageNum"))
                {
                    var p = countCmd.CreateParameter();
                    p.ParameterName = param.name;
                    p.Value = param.value;
                    countCmd.Parameters.Add(p);
                }
                var totalCount = 1000; //Convert.ToInt32(await countCmd.ExecuteScalarAsync());

                // Data query with pagination
                var dataQuery = $@"
            {baseQuery}
            ORDER BY s.id DESC
            LIMIT @PageSize OFFSET @PageNum";

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
                $"<a {actionData} data-type='OCRFile' href='#'>See OCR File</a>",
                $"<a {actionData} data-type='LivenesssOutput' href='#'>See Liveness Output</a>",
                $"<a {actionData} data-type='AuditContent' href='#'>See Audit Content</a>",
                $"<a {actionData} data-type='LineInformation' href='#'>See Line Information</a>",
                $"<a {actionData} data-type='Media' href='#'>See Media</a>",
                $"<a {actionData} data-type='SessionSteps' href='#'>See Session Steps</a>",
                currentStep,
                channelName, // Added channel information
                $"<a href='http://192.168.190.36/#/events?filter={uid}' target='_blank'>Go To Logs</a>",
                creationTime,
                status
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
            return View("Summary");
        }

        private async Task<TRCSessionModel> GetTRCReportData(bool isToday)
        {
            DateTime timespan = isToday ? DateTime.Today : DateTime.Now.AddDays(-30); // Changed from Dec 2024 to last 30 days
            return await GetRevampedTRCReportData(timespan);
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
}