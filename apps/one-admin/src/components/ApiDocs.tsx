import { ApiReferenceReact } from '@scalar/api-reference-react';
import { Menu } from 'lucide-react';
// Import the generated OpenAPI spec as a raw string
import openapiSpec from '../../../../packages/api-spec/dist/openapi.yaml?raw';
import '@scalar/api-reference-react/style.css';

interface ApiDocsProps {
  isMobile?: boolean;
  toggleSidebar?: () => void;
}

export default function ApiDocs({ isMobile, toggleSidebar }: ApiDocsProps) {
  return (
    <div className="flex-1 w-full flex flex-col h-full overflow-hidden bg-white">
      {/* Optional Header for Mobile Sidebar Toggle */}
      {isMobile && (
        <div className="flex items-center gap-3 p-4 border-b border-[#e5e5e5] shrink-0">
          <button 
            onClick={toggleSidebar}
            className="p-1.5 -ml-1.5 rounded-md text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] focus:outline-none transition-colors"
          >
            <Menu size={20} />
          </button>
          <h1 className="text-[16px] font-semibold text-[#09090b]">API Reference</h1>
        </div>
      )}

      {/* Scalar API Reference */}
      <div className="flex-1 overflow-auto custom-scalar-theme">
        <ApiReferenceReact 
          configuration={{
            spec: {
              content: openapiSpec
            },
            theme: 'default',
            showSidebar: true,
            hideDownloadButton: false,
            darkMode: false, // Set to true if your admin is dark mode
            metaData: {
              title: "Lazuar API Documentation"
            }
          }}
        />
      </div>
    </div>
  );
}
