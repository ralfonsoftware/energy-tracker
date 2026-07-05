import { useEffect, useRef } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import { EuroBurnGradient } from './EuroBurnGradient'
import { BottomTabBar } from './BottomTabBar'
import { SidebarNav } from './SidebarNav'
import { Header } from './Header'

export default function AppShell() {
  const mainRef = useRef<HTMLElement>(null)
  const { pathname, search, hash } = useLocation()

  useEffect(() => {
    mainRef.current?.scrollTo(0, 0)
  }, [pathname, search, hash])

  return (
    <>
      <EuroBurnGradient />
      <div className="flex min-h-screen">
        <div className="hidden md:flex flex-col w-[200px] sticky top-0 h-screen shrink-0">
          <SidebarNav />
        </div>
        <main
          ref={mainRef}
          className="flex-1 overflow-y-auto pb-[calc(84px_+_env(safe-area-inset-bottom,0px))] md:pb-0"
        >
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
