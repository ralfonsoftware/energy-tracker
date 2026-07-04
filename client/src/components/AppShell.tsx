import { Outlet } from 'react-router-dom'
import { EuroBurnGradient } from './EuroBurnGradient'
import { BottomTabBar } from './BottomTabBar'
import { SidebarNav } from './SidebarNav'
import { Header } from './Header'

export default function AppShell() {
  return (
    <>
      <EuroBurnGradient />
      <div className="flex min-h-screen">
        <div className="hidden md:flex flex-col w-[200px] sticky top-0 h-screen shrink-0">
          <SidebarNav />
        </div>
        <main className="flex-1 overflow-y-auto pb-[84px] md:pb-0">
          <Header />
          <Outlet />
        </main>
      </div>
      <div className="md:hidden">
        <BottomTabBar />
      </div>
    </>
  )
}
