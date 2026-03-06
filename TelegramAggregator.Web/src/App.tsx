import { BrowserRouter, Routes, Route } from 'react-router-dom';
import ChannelsPage from './pages/ChannelsPage';
import TelegramLoginPage from './pages/TelegramLoginPage';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<ChannelsPage />} />
        <Route path="/settings/telegram-login" element={<TelegramLoginPage />} />
      </Routes>
    </BrowserRouter>
  );
}
