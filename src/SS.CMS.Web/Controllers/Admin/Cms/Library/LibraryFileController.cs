﻿using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SS.CMS.Abstractions;
using SS.CMS.Abstractions.Dto.Result;
using SS.CMS.Core;
using SS.CMS.Extensions;

namespace SS.CMS.Web.Controllers.Admin.Cms.Library
{
    [Route("admin/cms/library/libraryFile")]
    public partial class LibraryFileController : ControllerBase
    {
        private const string Route = "";
        private const string RouteId = "{id:int}";
        private const string RouteDownload = "{siteId}/{libraryId}/{fileName}";
        private const string RouteList = "list";
        private const string RouteGroups = "groups";
        private const string RouteGroupId = "groups/{id}";

        private readonly ISettingsManager _settingsManager;
        private readonly IAuthManager _authManager;
        private readonly IPathManager _pathManager;
        private readonly ISiteRepository _siteRepository;
        private readonly ILibraryGroupRepository _libraryGroupRepository;
        private readonly ILibraryFileRepository _libraryFileRepository;

        public LibraryFileController(ISettingsManager settingsManager, IAuthManager authManager, IPathManager pathManager, ISiteRepository siteRepository, ILibraryGroupRepository libraryGroupRepository, ILibraryFileRepository libraryFileRepository)
        {
            _settingsManager = settingsManager;
            _authManager = authManager;
            _pathManager = pathManager;
            _siteRepository = siteRepository;
            _libraryGroupRepository = libraryGroupRepository;
            _libraryFileRepository = libraryFileRepository;
        }

        [HttpPost, Route(RouteList)]
        public async Task<ActionResult<QueryResult>> List([FromBody]QueryRequest req)
        {
            

            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSitePermissionsAsync(req.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            var groups = await _libraryGroupRepository.GetAllAsync(LibraryType.File);
            groups.Insert(0, new LibraryGroup
            {
                Id = 0,
                LibraryType = LibraryType.File,
                GroupName = "全部文件"
            });
            var count = await _libraryFileRepository.GetCountAsync(req.GroupId, req.Keyword);
            var items = await _libraryFileRepository.GetAllAsync(req.GroupId, req.Keyword, req.Page, req.PerPage);

            return new QueryResult
            {
                Groups = groups,
                Count = count,
                Items = items
            };
        }

        [HttpPost, Route(Route)]
        public async Task<ActionResult<LibraryFile>> Create([FromQuery] CreateRequest request, [FromForm] IFormFile file)
        {
            

            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            var site = await _siteRepository.GetAsync(request.SiteId);

            if (file == null)
            {
                return this.Error("请选择有效的文件上传");
            }

            var fileName = Path.GetFileName(file.FileName);

            var fileType = PathUtils.GetExtension(fileName);
            if (!_pathManager.IsUploadExtensionAllowed(UploadType.File, site, fileType))
            {
                return this.Error("文件只能是图片格式，请选择有效的文件上传!");
            }

            var libraryFileName = PathUtils.GetLibraryFileName(fileName);
            var virtualDirectoryPath = PathUtils.GetLibraryVirtualDirectoryPath(UploadType.File);
            
            var directoryPath = PathUtils.Combine(_settingsManager.WebRootPath, virtualDirectoryPath);
            var filePath = PathUtils.Combine(directoryPath, libraryFileName);

            await _pathManager.UploadAsync(file, filePath);

            var library = new LibraryFile
            {
                GroupId = request.GroupId,
                Title = PathUtils.RemoveExtension(fileName),
                FileType = fileType.ToUpper().Replace(".", string.Empty),
                Url = PageUtils.Combine(virtualDirectoryPath, libraryFileName)
            };

            await _libraryFileRepository.InsertAsync(library);

            return library;
        }

        [HttpPut, Route(RouteId)]
        public async Task<ActionResult<LibraryFile>> Update([FromBody] UpdateRequest request)
        {
            

            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            var lib = await _libraryFileRepository.GetAsync(request.Id);
            lib.Title = request.Title;
            lib.GroupId = request.GroupId;
            await _libraryFileRepository.UpdateAsync(lib);

            return lib;
        }

        [HttpDelete, Route(RouteId)]
        public async Task<ActionResult<BoolResult>> Delete([FromBody]DeleteRequest request)
        {
            

            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            await _libraryFileRepository.DeleteAsync(request.Id);

            return new BoolResult
            {
                Value = true
            };
        }

        [HttpGet, Route(RouteDownload)]
        public async Task<ActionResult> ActionsDownload([FromQuery]DownloadRequest request)
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.Library))
            {
                 return Unauthorized();
            }
            
            var library = await _libraryFileRepository.GetAsync(request.LibraryId);
            var filePath = _pathManager.GetLibraryFilePath(library.Url);
            return this.Download(filePath);
        }

        [HttpPost, Route(RouteGroups)]
        public async Task<ActionResult<LibraryGroup>> CreateGroup([FromBody] GroupRequest group)
        {
            

            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSitePermissionsAsync(group.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            var libraryGroup = new LibraryGroup
            {
                LibraryType = LibraryType.File,
                GroupName = group.Name
            };
            libraryGroup.Id = await _libraryGroupRepository.InsertAsync(libraryGroup);

            return libraryGroup;
        }

        [HttpPut, Route(RouteGroupId)]
        public async Task<ActionResult<LibraryGroup>> RenameGroup([FromQuery]int id, [FromBody] GroupRequest group)
        {
            

            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSitePermissionsAsync(group.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            var libraryGroup = await _libraryGroupRepository.GetAsync(id);
            libraryGroup.GroupName = group.Name;
            await _libraryGroupRepository.UpdateAsync(libraryGroup);

            return libraryGroup;
        }

        [HttpDelete, Route(RouteGroupId)]
        public async Task<ActionResult<BoolResult>> DeleteGroup([FromBody]DeleteGroupRequest request)
        {
            

            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            await _libraryGroupRepository.DeleteAsync(LibraryType.File, request.Id);

            return new BoolResult
            {
                Value = true
            };
        }
    }
}
