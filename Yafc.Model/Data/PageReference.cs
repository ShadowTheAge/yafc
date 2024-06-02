using System;

namespace Yafc.Model {
    public sealed class PageReference(Guid guid) {
        public PageReference(ProjectPage page) : this(page.guid) {
            _page = page;
        }

        public Guid guid { get; } = guid;
        private ProjectPage? _page;

        public ProjectPage? page {
            get {
                if (_page == null) {
                    _page = Project.current.FindPage(guid);
                }
                else if (_page.deleted) {
                    return null;
                }

                return _page;
            }
        }
    }
}
