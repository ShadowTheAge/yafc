using System;

namespace YAFC.Model {
    public sealed class PageReference {
        public PageReference(ProjectPage page) : this(page.guid) {
            _page = page;
        }

        public PageReference(Guid guid) {
            this.guid = guid;
        }

        public Guid guid { get; }
        private ProjectPage _page;

        public ProjectPage page {
            get {
                if (_page == null)
                    _page = Project.current.FindPage(guid);
                else if (_page.deleted)
                    return null;
                return _page;
            }
        }
    }
}
