import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"

export default function LoginForm({ onSubmit, isLoading, error }) {
  const handleSubmit = (event) => {
    event.preventDefault()
    if (!onSubmit) {
      return
    }
    const formData = new FormData(event.currentTarget)
    const username = (formData.get("username") ?? "").toString().trim()
    const password = (formData.get("password") ?? "").toString()
    onSubmit({ username, password })
  }

  return (
    <Card className="w-full max-w-sm">
      <form className="flex flex-col" onSubmit={handleSubmit}>
        <CardHeader>
          <CardTitle className="text-2xl">Login</CardTitle>
          <CardDescription>
            Enter your name below to login to your account.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4">
          <div className="grid gap-2">
            <Label htmlFor="username">Username</Label>
            <Input
              id="username"
              name="username"
              type="text"
              autoComplete="username"
              placeholder="name"
              required
            />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="password">Password</Label>
            <Input
              id="password"
              name="password"
              type="password"
              autoComplete="current-password"
              placeholder="••••••••"
              required
            />
          </div>
        </CardContent>
        {error && (
          <div className="px-6 text-sm text-destructive-foreground" role="alert" aria-live="polite">
            {error}
          </div>
        )}
        <CardFooter>
          <Button className="w-full" disabled={isLoading} type="submit">
            {isLoading ? "Signing in…" : "Sign in"}
          </Button>
        </CardFooter>
      </form>
    </Card>
  )
}
