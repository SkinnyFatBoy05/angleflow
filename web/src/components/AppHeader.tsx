import { FileText, GitBranch, Plus, Settings } from "lucide-react";

interface AppHeaderProps {
  onNewApplication: () => void;
}

function BrandMark() {
  return (
    <svg className="brand-mark" viewBox="0 0 36 36" aria-hidden="true">
      <path d="M18 3 32 30h-9L18 19l-5 11H4L18 3Z" fill="currentColor" />
      <path d="m18 3 5 10-5 10-5-10 5-10Z" fill="#70a5ff" />
    </svg>
  );
}

export function AppHeader({ onNewApplication }: AppHeaderProps) {
  return (
    <header className="app-header">
      <a className="brand" href="#main" aria-label="AngleFlow home">
        <BrandMark />
        <span>AngleFlow</span>
      </a>
      <nav className="primary-nav" aria-label="Primary navigation">
        <a className="nav-link active" href="#applications"><FileText aria-hidden="true" />Applications</a>
        <a className="nav-link" href="#workflow"><GitBranch aria-hidden="true" />Workflow</a>
        <a className="nav-link" href="#operations"><Settings aria-hidden="true" />Operations</a>
      </nav>
      <button className="button button-primary header-action" type="button" onClick={onNewApplication}>
        <Plus aria-hidden="true" />
        <span>New application</span>
      </button>
    </header>
  );
}
