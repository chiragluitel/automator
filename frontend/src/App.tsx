import { Routes, Route } from "react-router-dom";
import { AutomationsListPage } from "@/pages/AutomationsListPage";
import { AutomationBuilderPage } from "@/pages/AutomationBuilderPage";
import { AutomationDetailPage } from "@/pages/AutomationDetailPage";

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<AutomationsListPage />} />
      <Route path="/new" element={<AutomationBuilderPage />} />
      <Route path="/automations/:id" element={<AutomationDetailPage />} />
    </Routes>
  );
}
